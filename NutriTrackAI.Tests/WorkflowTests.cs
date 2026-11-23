using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NutriTrackAI.Controllers;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

public class WorkflowTests : IAsyncLifetime
{
    [Fact] public async Task OnboardingPreview_AllowsMissingOptionalWaterField()
    {
        await Login();
        var token = Token(await Page("/Onboarding"));
        var input = new { Gender = "Male", DateOfBirth = GoalPolicy.Today.AddYears(-24).ToString("yyyy-MM-dd"),
            HeightCm = 170, CurrentWeightKg = 72, TargetWeightKg = 70, DurationWeeks = 6,
            GoalType = "Lose", ActivityLevel = "Moderate", DietType = "Normal",
            Allergies = "", DislikedFoods = "", HealthNotes = "", Screening = new HealthScreeningForm { NoConditions = true } };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Onboarding/CalculatePreview");
        request.Headers.Add("RequestVerificationToken", token);
        request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(input), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.False(json.RootElement.GetProperty("isBlocked").GetBoolean());
        Assert.Equal(2218, json.RootElement.GetProperty("dailyCalorieTarget").GetInt32());
        Assert.Equal(4, json.RootElement.GetProperty("recommendedWeeks").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("85")]
    [InlineData("85.5")]
    [InlineData("84.5")]
    public async Task GoalPreview_HidesEnergyDetailsForInvalidTargetDirectionOrDistance(string target)
    {
        await Login();
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.CurrentWeightKg = 85;
        await db.SaveChangesAsync();

        using var response = await Post("/Goal/Preview", Token(await Page("/Goal/Create")),
            ("GoalType", "Lose"), ("TargetWeight", target), ("DurationWeeks", "19"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(GoalPolicy.GoalTargetPrompt, html);
        Assert.DoesNotContain("<dl", html);
        Assert.DoesNotContain("Chưa thể ước tính", html);
    }

    [Fact]
    public async Task GoalPreview_ShowsOnlyScreeningReasonAndTheReturnLinkWhenBlocked()
    {
        await Login();
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.HealthNotes = "Ghi chú cũ";
        profile.HealthScreeningAt = null;
        await db.SaveChangesAsync();

        using var response = await Post("/Goal/Preview", Token(await Page("/Goal/Create")),
            ("GoalType", "Lose"), ("TargetWeight", "65"), ("DurationWeeks", "16"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(GoalPolicy.CompleteHealthScreening, html);
        Assert.Contains("/Profile/Screening?returnUrl=", html);
        Assert.Contains("/Goal/Create?resume=goal", Uri.UnescapeDataString(html));
        Assert.DoesNotContain("<dl", html);
        Assert.DoesNotContain("Cần đánh giá lại cân nặng đích", html);
    }

    [Theory]
    [InlineData("/Goal/Create?resume=goal", "/Goal/Create?resume=goal")]
    [InlineData("/Onboarding?resume=goal", "/Onboarding?resume=goal")]
    [InlineData("http://evil.example", "/Dashboard")]
    [InlineData("//evil.example", "/Dashboard")]
    [InlineData("", "/Dashboard")]
    public async Task ScreeningSave_ReturnsOnlyToLocalUrls(string requested, string expected)
    {
        await Login();
        var token = Token(await Page("/Profile/Screening?returnUrl=" + Uri.EscapeDataString(requested)));
        using var response = await Post("/Profile/Screening", token, ("ReturnUrl", requested),
            ("Screening.NoConditions", "true"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ScreeningSave_RequiresAFreshDoctorConfirmationOnlyWhenTheRelevantConditionsChange()
    {
        await Login();
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.MedicalConditions = MedicalConditions.Hypertension;
        profile.DoctorAwareOfPlan = true;
        profile.HealthScreeningAt = GoalPolicy.Now;
        await db.SaveChangesAsync();

        var token = Token(await Page("/Profile/Screening"));
        async Task<bool> Save(params string[] conditions)
        {
            using var response = await Post("/Profile/Screening", token,
                [("Screening.DoctorAwareOfPlan", "true"), .. conditions.Select(condition => ("Screening.Conditions", condition))]);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            return await db.UserProfiles.Where(p => p.UserId == userId).Select(p => p.DoctorAwareOfPlan).SingleAsync();
        }

        Assert.True(await Save("Hypertension", "Gastritis"));
        Assert.False(await Save("Hypertension", "Diabetes", "Gastritis"));
        Assert.True(await Save("Hypertension", "Diabetes", "Gastritis"));
    }

    [Fact]
    public async Task ChatInput_DescribesTheSensitiveInformationNotice()
    {
        await Login();
        var html = WebUtility.HtmlDecode(await Page("/Chat"));
        Assert.Contains("aria-describedby=\"chatPrivacyNotice\"", html);
        Assert.Contains("Đừng nhập thông tin cá nhân nhạy cảm (họ tên, bệnh, thuốc) vào cuộc trò chuyện.", html);
    }

    [Fact]
    public async Task PaceOptions_AgreeAcrossFormsAndMatchEachSavedBudget()
    {
        await Login();
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.CurrentWeightKg = 85;
        profile.TargetWeightKg = 80;
        profile.HeightCm = 170;
        profile.DateOfBirth = GoalPolicy.Today.AddYears(-24);
        profile.ActivityLevel = ActivityLevel.Moderate;
        profile.HealthScreeningAt = GoalPolicy.Now;
        await db.SaveChangesAsync();

        var goalPage = WebUtility.HtmlDecode(await Page("/Goal/Create"));
        Assert.Contains("Chọn thời gian đạt mục tiêu", goalPage);
        Assert.Contains("Đề xuất", goalPage.Split("<template")[0]);
        Assert.Contains("Ăn khoảng 2.236 kcal/ngày", goalPage);
        var token = Token(goalPage);
        (string, string)[] GoalFields(int weeks, bool acknowledge = false) =>
        [
            ("GoalType", "Lose"), ("TargetWeight", "80"), ("DurationWeeks", weeks.ToString(CultureInfo.InvariantCulture)),
            ("AcknowledgeIntensePace", acknowledge ? "true" : "false")
        ];
        async Task<JsonDocument> GoalOptions()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Goal/Preview");
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = new FormUrlEncodedContent(GoalFields(20).Append(("__RequestVerificationToken", token))
                .Select(field => KeyValuePair.Create(field.Item1, field.Item2)));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        using var goalPreview = await GoalOptions();
        using var onboardingRequest = new HttpRequestMessage(HttpMethod.Post, "/Onboarding/CalculatePreview");
        onboardingRequest.Headers.Add("RequestVerificationToken", Token(await Page("/Onboarding")));
        onboardingRequest.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            Gender = "Male", DateOfBirth = profile.DateOfBirth.ToString("yyyy-MM-dd"), HeightCm = 170,
            CurrentWeightKg = 85, TargetWeightKg = 80, DurationWeeks = 20, GoalType = "Lose",
            ActivityLevel = "Moderate", Screening = new { NoConditions = true }
        });
        using var onboardingResponse = await client.SendAsync(onboardingRequest);
        Assert.Equal(HttpStatusCode.OK, onboardingResponse.StatusCode);
        using var onboardingPreview = JsonDocument.Parse(await onboardingResponse.Content.ReadAsStringAsync());
        var options = goalPreview.RootElement.GetProperty("paceOptions");
        Assert.Equal(options.GetRawText(), onboardingPreview.RootElement.GetProperty("paceOptions").GetRawText());

        foreach (var option in options.EnumerateArray())
        {
            Assert.True(option.GetProperty("isAvailable").GetBoolean());
            using var saved = await Post("/Goal/Create", token, GoalFields(option.GetProperty("weeks").GetInt32(),
                option.GetProperty("requiresIntensePaceAck").GetBoolean()));
            Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            var active = await db.Goals.AsNoTracking().SingleAsync(goal => goal.UserId == userId && goal.IsActive);
            Assert.Equal(option.GetProperty("dailyBudgetKcal").GetInt32(), active.DailyCalorieTarget);
        }

        profile.MedicalConditions = MedicalConditions.Hypertension;
        profile.DoctorAwareOfPlan = true;
        await db.SaveChangesAsync();
        using var capped = await GoalOptions();
        foreach (var option in capped.RootElement.GetProperty("paceOptions").EnumerateArray()
                     .Where(option => !option.GetProperty("isAvailable").GetBoolean()))
        {
            using var rejected = await Post("/Goal/Create", token, GoalFields(option.GetProperty("weeks").GetInt32(), true));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Contains("vượt giới hạn cho hồ sơ", WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync()));
        }
    }
    private WebApplication app = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        app = await QaApplication.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = new Uri(address) };
    }
    public async Task DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); }
    private async Task<string> Page(string path)
    {
        var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode}\n{html[..Math.Min(html.Length, 1800)]}");
        return html;
    }
    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private async Task Login(string email = "qa-a@example.test")
    {
        var token = Token(await Page("/Account/Login"));
        var response = await Post("/Account/Login", token, ("Email", email), ("Password", "QaPass123!"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private Task<HttpResponseMessage> Post(string path, string token, params (string Key, string Value)[] fields)
        => client.PostAsync(path, new FormUrlEncodedContent(fields.Append(("__RequestVerificationToken", token))
            .Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2))));
    private async Task<int> UserId(string email = "qa-a@example.test")
    {
        using var scope = app.Services.CreateScope(); return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Email == email)).Id;
    }
    private static TController ControllerFor<TController>(IServiceProvider services, int userId) where TController : Controller
    {
        var controller = ActivatorUtilities.CreateInstance<TController>(services);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "QA"))
            }
        };
        return controller;
    }
    private async Task<(int Id, string Fingerprint, decimal Adjustment)> ReviewData()
    {
        var user = await UserId(); using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.SingleAsync(g => g.UserId == user && g.IsActive);
        var review = await scope.ServiceProvider.GetRequiredService<AdaptiveCalorieService>().ReviewAsync(user);
        Assert.NotNull(review.ProposedAdjustment);
        return (goal.Id, review.Fingerprint, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("qa-a@example.test")]
    [InlineData("qa-admin@example.test")]
    public async Task HomeHero_ShowsRequestedActionsOnFirstTwoSlides(string? email)
    {
        if (email != null)
            await Login(email);

        var home = await Page("/");
        var slides = Regex.Matches(home,
            "<div class=\"carousel-item\\b[^>]*>.*?(?=<div class=\"(?:carousel-item\\b|home-carousel-toolbar\"))",
            RegexOptions.Singleline);
        Assert.Equal(3, slides.Count);
        for (var i = 0; i < 2; i++)
        {
            var links = Regex.Matches(slides[i].Value, "<a\\b[^>]*\\bhref=\"([^\"]+)\"")
                .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value));
            Assert.Equal(new[] { "/Account/Register", "/Chat/Index" }, links);
        }
        Assert.DoesNotContain("<a ", slides[2].Value);
        Assert.Contains("class=\"home-hero-accent\"", slides[2].Value);

        if (email == null)
            await Page("/Account/Register");
    }

    [Theory]
    [InlineData(false, "/", "/", "page")]
    [InlineData(false, "/Home/Articles", "/Home/Articles", "page")]
    [InlineData(true, "/Dashboard", "/Dashboard", "page")]
    [InlineData(true, "/Goal/Create", "/WeightLog", "location")]
    [InlineData(true, "/Food/Details/1", "/Food", "location")]
    [InlineData(true, "/Profile", "/Profile", "page")]
    public async Task MainNavigation_IdentifiesCurrentPageOrSection(bool signedIn, string path, string expectedLink, string expectedState)
    {
        if (signedIn)
            await Login();

        var html = await Page(path);
        var nav = Regex.Match(html, "<nav\\b[^>]*class=\"navbar[^>]*>.*?</nav>", RegexOptions.Singleline);
        Assert.True(nav.Success);
        var currentLink = Assert.Single(Regex.Matches(nav.Value, "<a\\b[^>]*aria-current=\"[^\"]+\"[^>]*>")
            .Select(match => match.Value));
        Assert.Contains($"href=\"{expectedLink}\"", currentLink);
        Assert.Contains($"aria-current=\"{expectedState}\"", currentLink);
    }

    [Theory] [InlineData("/Goal")] [InlineData("/Profile")] [InlineData("/Onboarding")]
    public async Task GoalAndScreeningPages_RequireAuthentication(string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }
    [Theory] [InlineData("/Goal/Create")] [InlineData("/Goal/Preview")] [InlineData("/Goal/Review")] [InlineData("/Goal/Extend")]
    [InlineData("/Goal/BodyCheckIn")] [InlineData("/Goal/ConfirmBodyAdjustment")] [InlineData("/Profile")] [InlineData("/Onboarding/Complete")]
    public async Task GoalPosts_RequireAntiForgery(string path)
    {
        await Login(); var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Theory] [InlineData("/Goal")] [InlineData("/Goal/Create")] [InlineData("/Dashboard")]
    [InlineData("/Profile")] [InlineData("/WeightLog")] [InlineData("/MealLog")] [InlineData("/MealPlan")] [InlineData("/Chat")]
    public async Task ExistingPages_RenderAfterLogin(string path) { await Login(); await Page(path); }

    [Fact]
    public async Task BodyCheckIn_KeepDismissesBannerButLeavesOnDemandAction()
    {
        await Login();
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.AsNoTracking().SingleAsync(g => g.UserId == user && g.IsActive);
        var before = await Page("/Dashboard");
        Assert.Contains("data-body-check-in-due", before);
        var response = await Post("/Goal/BodyCheckIn", Token(before), ("id", goal.Id.ToString()), ("feedback", "Keep"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = await Page("/Goal");
        Assert.DoesNotContain("data-body-check-in-due", after);
        Assert.Contains("id=\"body-check-in\"", after);
        var saved = await db.Goals.AsNoTracking().SingleAsync(g => g.Id == goal.Id);
        Assert.Equal(goal.TargetDate, saved.TargetDate);
        Assert.Equal(goal.DailyCalorieTarget, saved.DailyCalorieTarget);
        Assert.Equal(GoalPolicy.Today, CaloriePlanState.Read(saved.CaloriePlanJson)!.LastBodyCheckInOn);
        var preview = await Post("/Goal/BodyCheckIn", Token(after), ("id", goal.Id.ToString()), ("feedback", "Increase"));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    }

    [Fact]
    public async Task BodyAdjustment_PreviewsBeforeSavingAndSynchronizesBudget()
    {
        await Login();
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.AsNoTracking().SingleAsync(g => g.UserId == user && g.IsActive);
        var page = await Page("/Goal");
        var response = await Post("/Goal/BodyCheckIn", Token(page), ("id", goal.Id.ToString()), ("feedback", "Decrease"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"fingerprint\"", html);
        var fingerprint = Regex.Match(html, "name=\"fingerprint\" value=\"([A-Za-z0-9_-]+)\"").Groups[1].Value;
        Assert.NotEmpty(fingerprint);
        Assert.Equal(goal.CaloriePlanJson, (await db.Goals.AsNoTracking().SingleAsync(g => g.Id == goal.Id)).CaloriePlanJson);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/Goal/ConfirmBodyAdjustment", Token(html),
            ("id", goal.Id.ToString()), ("feedback", "Decrease"), ("fingerprint", "forged"))).StatusCode);
        var confirmFields = new[] { ("id", goal.Id.ToString()), ("feedback", "Decrease"), ("fingerprint", fingerprint) };
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Goal/ConfirmBodyAdjustment", Token(html), confirmFields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post("/Goal/ConfirmBodyAdjustment", Token(html), confirmFields)).StatusCode);
        var saved = await db.Goals.AsNoTracking().SingleAsync(g => g.Id == goal.Id);
        var state = CaloriePlanState.Read(saved.CaloriePlanJson)!;
        Assert.Equal(325m, state.Adjustment);
        Assert.Single(state.Changes);
        Assert.Equal(.30m, saved.WeeklyRateKg);
        Assert.Equal(GoalPolicy.Today.AddDays(159), saved.TargetDate);
        Assert.Equal(2255, saved.DailyCalorieTarget);
        var context = await scope.ServiceProvider.GetRequiredService<IUserContextBuilder>().BuildUserContextAsync(user);
        var dashboard = ControllerFor<DashboardController>(scope.ServiceProvider, user);
        var model = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await dashboard.Index()).Model);
        Assert.Equal(saved.DailyCalorieTarget, context.DailyCalorieTarget);
        Assert.Equal(saved.DailyCalorieTarget, model.DailyCalorieTarget);
        Assert.Equal(context.RemainingCalories, model.RemainingCalories);
    }

    [Fact] public async Task Create_IgnoresClientCaloriesTdeeAndIdentity()
    {
        await Login(); var token = Token(await Page("/Goal/Create")); var user = await UserId(); var other = await UserId("qa-b@example.test");
        var response = await Post("/Goal/Create", token, ("GoalType", "Lose"), ("TargetWeight", "65"), ("DurationWeeks", "16"),
            ("StartWeight", "299"), ("HeightCm", "250"), ("CalculatedDailyCalories", "999"), ("Tdee", "9000"), ("UserId", other.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.SingleAsync(g => g.UserId == user && g.IsActive);
        Assert.Equal(71.7m, goal.StartWeight); Assert.Equal(2119, goal.DailyCalorieTarget);
        Assert.Equal(460.625m, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
        Assert.Single(await db.Goals.Where(g => g.UserId == other).ToListAsync());
    }
    [Fact] public async Task ImpossibleDeadline_DoesNotReplaceActiveGoal()
    {
        await Login(); var token = Token(await Page("/Goal/Create"));
        var response = await Post("/Goal/Create", token, ("GoalType", "Lose"), ("TargetWeight", "65"), ("DurationWeeks", "9"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.Goals.CountAsync());
        Assert.Contains("value=\"9\"", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task GoalPreview_ExplainsWhyAOneWeekWeightChangeIsBlocked()
    {
        await Login();
        var token = Token(await Page("/Goal/Create"));
        using var response = await Post("/Goal/Preview", token, ("GoalType", "Lose"),
            ("TargetWeight", "70.7"), ("DurationWeeks", "1"));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-blocked=\"true\"", html);
        Assert.Contains("2 tuần", html);
    }

    [Fact]
    public async Task GoalPace_PreviewAndCreateRequireAcknowledgmentWithoutBypassingTheCap()
    {
        await Login();
        var user = await UserId();
        var token = Token(await Page("/Goal/Create"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var activeGoals = db.Goals.AsNoTracking().Where(goal => goal.UserId == user && goal.IsActive);
        var previous = await activeGoals.SingleAsync();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        profile.DateOfBirth = GoalPolicy.Today.AddYears(-35);
        profile.HeightCm = 175;
        profile.CurrentWeightKg = 85;
        profile.ActivityLevel = ActivityLevel.Light;
        await db.SaveChangesAsync();

        (string Key, string Value)[] Form(int weeks, bool acknowledge) =>
        [
            ("GoalType", "Lose"), ("TargetWeight", "75"), ("DurationWeeks", weeks.ToString(CultureInfo.InvariantCulture)),
            ("AcknowledgeIntensePace", acknowledge ? "true" : "false")
        ];

        using var preview = await Post("/Goal/Preview", token, Form(13, false));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.StartsWith("text/html", preview.Content.Headers.ContentType!.MediaType);
        var previewHtml = await preview.Content.ReadAsStringAsync();
        Assert.Contains("data-pace-zone=\"Intense\"", previewHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-requires-intense-pace-ack=\"true\"", previewHtml);
        Assert.Contains("data-blocked=\"false\"", previewHtml);
        var encodedOptions = Regex.Match(previewHtml, "data-pace-options=\"([^\"]+)\"").Groups[1].Value;
        using var options = JsonDocument.Parse(WebUtility.HtmlDecode(encodedOptions));
        var fastest = Assert.Single(options.RootElement.EnumerateArray(), item => item.GetProperty("key").GetString() == "fastest");
        Assert.Equal(13, fastest.GetProperty("weeks").GetInt32());
        Assert.Contains("data-pace-zone-label=", previewHtml);
        Assert.Contains("data-max-weekly-rate-kg=", previewHtml);
        Assert.InRange(fastest.GetProperty("weeklyRateKg").GetDecimal(), .76m, .77m);

        using var jsonRequest = new HttpRequestMessage(HttpMethod.Post, "/Goal/Preview");
        jsonRequest.Headers.Accept.ParseAdd("application/json");
        jsonRequest.Content = new FormUrlEncodedContent(Form(13, false)
            .Append(("__RequestVerificationToken", token)).Select(field => KeyValuePair.Create(field.Item1, field.Item2)));
        using var jsonResponse = await client.SendAsync(jsonRequest);
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        using var previewData = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync());
        Assert.Equal("Intense", previewData.RootElement.GetProperty("paceZone").GetString());
        Assert.Equal("Mạnh", previewData.RootElement.GetProperty("paceZoneLabel").GetString());
        Assert.Equal(.78m, previewData.RootElement.GetProperty("maxWeeklyRateKg").GetDecimal());
        Assert.True(previewData.RootElement.GetProperty("requiresIntensePaceAck").GetBoolean());
        Assert.Equal("Clear", previewData.RootElement.GetProperty("healthTier").GetString());

        using var missingAck = await Post("/Goal/Create", token, Form(13, false));
        Assert.Equal(HttpStatusCode.OK, missingAck.StatusCode);
        var deniedHtml = await missingAck.Content.ReadAsStringAsync();
        Assert.Contains("field-validation-error", deniedHtml);
        Assert.Contains("data-valmsg-for=\"AcknowledgeIntensePace\"", deniedHtml);
        Assert.Contains(GoalPolicy.IntensePaceAcknowledgmentMessage, WebUtility.HtmlDecode(deniedHtml));
        Assert.Equal(previous.Id, (await activeGoals.SingleAsync()).Id);

        using var beyondCap = await Post("/Goal/Create", token, Form(12, true));
        Assert.Equal(HttpStatusCode.OK, beyondCap.StatusCode);
        Assert.Equal(previous.Id, (await activeGoals.SingleAsync()).Id);

        using var accepted = await Post("/Goal/Create", token, Form(13, true));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var saved = await activeGoals.SingleAsync();
        Assert.NotEqual(previous.Id, saved.Id);
        Assert.Equal(1593, saved.DailyCalorieTarget);
        Assert.Equal(.77m, saved.WeeklyRateKg);
        Assert.Equal(GoalPolicy.Today.AddDays(91), saved.TargetDate);
        Assert.InRange(CaloriePlanState.Read(saved.CaloriePlanJson)!.Adjustment, 846.1538m, 846.1539m);
    }
    [Theory] [InlineData("Review")] [InlineData("Extend")]
    public async Task CannotChangeOtherUsersGoal(string action)
    {
        await Login(); var token = Token(await Page("/Goal")); var other = await UserId("qa-b@example.test");
        using var scope = app.Services.CreateScope(); var goal = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Goals.SingleAsync(g => g.UserId == other);
        var response = await Post("/Goal/" + action, token, ("id", goal.Id.ToString()), ("weeks", "20"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact] public async Task AdminSelfTest_DoesNotReadPersonalHealth()
    {
        await Login("qa-admin@example.test");
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await UserId(); var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user); profile.Allergies = "PRIVATE_HEALTH_MARKER";
        profile.HealthScreeningJson = HealthScreening.Update(null, QaApplication.NoReportedFactors with { EatingDisorder = ScreeningAnswer.Yes });
        await db.SaveChangesAsync(); var html = await Page("/Admin/SystemSelfTest");
        Assert.DoesNotContain("PRIVATE_HEALTH_MARKER", html);
        Assert.DoesNotContain("qa-a@example.test", html);
        Assert.DoesNotContain(nameof(HealthScreeningAnswers.EatingDisorder), await Page("/Admin/Users"));
        var otherGoal = await db.Goals.SingleAsync(g => g.UserId == user);
        var response = await Post("/Goal/Review", Token(await Page("/Goal")), ("id", otherGoal.Id.ToString()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Theory] [InlineData(600)] [InlineData(630)]
    public async Task Review_RequiresConsent_ThenAppliesSafeAdjustment(int proposed)
    {
        await Login(); var token = Token(await Page("/Goal")); var review = await ReviewData();
        var unconfirmed = await Post("/Goal/Review", token, ("id", review.Id.ToString()), ("fingerprint", review.Fingerprint), ("apply", "true"), ("adjustment", proposed.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, unconfirmed.StatusCode);
        Assert.Equal(500m, (await ReviewData()).Adjustment);
        var confirmed = await Post("/Goal/Review", token, ("id", review.Id.ToString()), ("fingerprint", review.Fingerprint), ("apply", "true"), ("confirmed", "true"), ("adjustment", proposed.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.SingleAsync(g => g.Id == review.Id);
        Assert.Equal((decimal)proposed, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
        Assert.Equal(GoalPolicy.RoundCalories(2579.975m - proposed), goal.DailyCalorieTarget);
        var change = Assert.Single(CaloriePlanState.Read(goal.CaloriePlanJson)!.Changes);
        Assert.Equal(500m, change.PreviousAdjustment);
        Assert.Equal((decimal)proposed, change.Adjustment);
    }
    [Fact] public async Task Review_RejectsExcessiveAdjustment()
    {
        await Login(); var token = Token(await Page("/Goal")); var review = await ReviewData();
        var response = await Post("/Goal/Review", token, ("id", review.Id.ToString()), ("fingerprint", review.Fingerprint), ("apply", "true"), ("confirmed", "true"), ("adjustment", "1001"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(500m, (await ReviewData()).Adjustment);
    }
    [Fact]
    public async Task Review_RejectsAdjustmentAbovePersonalLimit()
    {
        await Login();
        var page = await Page("/Goal");
        Assert.DoesNotContain("<option value=\"650\"", page);
        Assert.DoesNotContain("<option value=\"700\"", page);
        var review = await ReviewData();
        using var response = await Post("/Goal/Review", Token(page), ("id", review.Id.ToString()),
            ("fingerprint", review.Fingerprint), ("apply", "true"), ("confirmed", "true"), ("adjustment", "650"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(500m, (await ReviewData()).Adjustment);
    }
    [Fact] public async Task Review_RejectsStaleProposal()
    {
        await Login(); var token = Token(await Page("/Goal")); var review = await ReviewData();
        var user = await UserId();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
            profile.CurrentWeightKg = 69; await db.SaveChangesAsync();
        }
        var response = await Post("/Goal/Review", token, ("id", review.Id.ToString()), ("fingerprint", review.Fingerprint), ("apply", "true"), ("confirmed", "true"), ("adjustment", "630"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var check = app.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<AppDbContext>().Goals.SingleAsync(g => g.Id == review.Id);
        Assert.Equal(500m, CaloriePlanState.Read(saved.CaloriePlanJson)!.Adjustment);
    }
    [Fact] public async Task KeepPlan_RecordsReviewWithoutChangingDeficit()
    {
        await Login(); var token = Token(await Page("/Goal")); var review = await ReviewData();
        var response = await Post("/Goal/Review", token, ("id", review.Id.ToString()), ("fingerprint", review.Fingerprint), ("apply", "false"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope(); var goal = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Goals.SingleAsync(g => g.Id == review.Id);
        var state = CaloriePlanState.Read(goal.CaloriePlanJson)!;
        Assert.Equal(500m, state.Adjustment); Assert.Equal(GoalPolicy.Today, state.LastReviewedOn);
    }
    [Fact] public async Task AllConsumers_UseSameBudget_AndPreserveFoodFilters()
    {
        var user = await UserId(); using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var today = GoalPolicy.Today;
        // Round the credit from the day's total once, since these entries would round up separately.
        db.ExerciseLogs.AddRange(
            new ExerciseLog { UserId = user, LoggedOn = today, DurationMin = 30, CaloriesBurned = 199m },
            new ExerciseLog { UserId = user, LoggedOn = today, DurationMin = 30, CaloriesBurned = 201m });
        await db.SaveChangesAsync();

        var budget = await services.GetRequiredService<CalorieBudgetService>().GetAsync(user);
        var context = await services.GetRequiredService<IUserContextBuilder>().BuildUserContextAsync(user, today);
        var food = await services.GetRequiredService<IFoodToolService>().GetTodayNutritionAsync(user, today);
        var remaining = await services.GetRequiredService<IFoodToolService>().GetRemainingCaloriesAsync(user, today);
        var meals = await services.GetRequiredService<IMealPlannerService>().GeneratePlanAsync(user, 999);
        var dashboardView = Assert.IsType<ViewResult>(await ControllerFor<DashboardController>(services, user).Index());
        var dashboard = Assert.IsType<DashboardViewModel>(dashboardView.Model);
        var diaryView = Assert.IsType<ViewResult>(await ControllerFor<MealLogController>(services, user).Index(today));
        var diary = Assert.IsType<MealLogIndexViewModel>(diaryView.Model);

        Assert.Equal(budget.TargetCalories, context.DailyCalorieTarget);
        Assert.Equal(budget.TargetCalories, food.DailyCalorieTarget); Assert.Equal(budget.TargetCalories, remaining.DailyTarget);
        Assert.Equal(budget.TargetCalories, meals.TargetCalories);
        Assert.Equal(400m, context.CaloriesBurnedToday);
        Assert.Equal(context.CaloriesBurnedToday, food.CaloriesBurned);
        Assert.Equal(context.CaloriesBurnedToday, remaining.Burned);
        Assert.Equal(context.CaloriesBurnedToday, dashboard.CaloriesBurnedToday);
        Assert.Equal(context.CaloriesBurnedToday, diary.CaloriesBurnedToday);
        Assert.Equal(200m, context.ExerciseCreditApplied);
        Assert.Equal(context.ExerciseCreditApplied, food.ExerciseCreditApplied);
        Assert.Equal(context.ExerciseCreditApplied, remaining.ExerciseCreditApplied);
        Assert.Equal(context.ExerciseCreditApplied, dashboard.ExerciseCreditApplied);
        Assert.Equal(context.ExerciseCreditApplied, diary.ExerciseCreditApplied);
        Assert.Equal(budget.TargetCalories - context.CaloriesConsumedToday + 200m, context.RemainingCalories);
        Assert.Equal(context.RemainingCalories, food.RemainingCalories);
        Assert.Equal(context.RemainingCalories, remaining.Remaining);
        Assert.Equal(context.RemainingCalories, dashboard.RemainingCalories);
        Assert.Equal(context.RemainingCalories, diary.RemainingCalories);
        Assert.Equal(budget.TargetCalories, meals.Meals.Sum(m => m.TargetCalories));
        Assert.Equal(GoalPolicy.RoundCalories(budget.TargetCalories * .25m), meals.Meals.Single(m => m.MealType == MealType.Breakfast).TargetCalories);
        Assert.All(meals.Meals.SelectMany(m => m.Items), item => Assert.InRange(item.FoodId, 1, 15));
        var contextBuilder = services.GetRequiredService<IUserContextBuilder>();
        var prompt = AiPromptBuilder.Build(context, await contextBuilder.BuildSystemPromptContextAsync(user));
        Assert.Contains("500", prompt); Assert.Contains("Không tự", prompt); Assert.Contains("dự kiến", prompt);
        Assert.Contains("400 kcal", prompt);
        Assert.Contains("200 kcal", prompt);
    }

    [Fact]
    public async Task AddExercise_StoresNetCalories_AndPreservesHistoricalRecord()
    {
        await Login();
        var token = Token(await Page("/WaterExercise"));
        var today = GoalPolicy.Today;
        var user = await UserId();
        using (var seedScope = app.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ExerciseLogs.Add(new ExerciseLog
            {
                UserId = user, LoggedOn = today.AddDays(-1), DurationMin = 30,
                CustomName = "Bản ghi cũ", CaloriesBurned = 188.2m
            });
            await db.SaveChangesAsync();
        }

        using var response = await Post("/WaterExercise/AddExercise", token,
            ("LoggedOn", today.ToString("yyyy-MM-dd")), ("DurationMin", "30"),
            ("Intensity", "Moderate"), ("CustomName", "Bản ghi mới"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var checkScope = app.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await check.ExerciseLogs.AsNoTracking().Where(log => log.UserId == user).ToListAsync();
        Assert.Equal(188.2m, Assert.Single(logs, log => log.LoggedOn == today.AddDays(-1)).CaloriesBurned);
        Assert.Equal(150.6m, Assert.Single(logs, log => log.LoggedOn == today).CaloriesBurned);
    }

    [Theory]
    [InlineData(true, "Low", 30, 134.4)]
    [InlineData(true, "Moderate", 30, 252)]
    [InlineData(true, "High", 30, 428.4)]
    [InlineData(true, "High", 60, 856.8)]
    [InlineData(false, "Low", 30, 84)]
    [InlineData(false, "Moderate", 30, 168)]
    [InlineData(false, "High", 30, 294)]
    public async Task AddExercise_AppliesIntensityAndDurationToCatalogueAndCustomActivities(
        bool catalogue, string intensity, int duration, double expected)
    {
        await Login();
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserProfiles.SingleAsync(p => p.UserId == userId)).CurrentWeightKg = 80m;
        await db.SaveChangesAsync();
        var exercise = await db.Exercises.SingleAsync();
        var date = GoalPolicy.Today.AddDays(-2);
        using var response = await Post("/WaterExercise/AddExercise", Token(await Page("/WaterExercise")),
            ("LoggedOn", date.ToString("yyyy-MM-dd")), ("ExerciseId", catalogue ? exercise.Id.ToString() : ""),
            ("DurationMin", duration.ToString()), ("Intensity", intensity), ("CustomName", "Leo cầu thang"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await db.ExerciseLogs.AsNoTracking().SingleAsync(l => l.UserId == userId && l.LoggedOn == date);
        Assert.Equal((decimal)expected, saved.CaloriesBurned);
        Assert.Equal(duration, saved.DurationMin);
        Assert.Equal(Enum.Parse<ExerciseIntensity>(intensity), saved.Intensity);
        Assert.Equal(catalogue ? (int?)exercise.Id : null, saved.ExerciseId);
        Assert.Equal(catalogue ? "Bóng đá" : "Leo cầu thang", saved.CustomName);
    }

    [Theory]
    [InlineData("Unknown", null)]
    [InlineData("99", null)]
    [InlineData("Low", -1)]
    [InlineData("High", 99999)]
    public async Task AddExercise_RejectsInvalidSelections(string intensity, int? exerciseId)
    {
        await Login();
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.ExerciseLogs.CountAsync();
        using var response = await Post("/WaterExercise/AddExercise", Token(await Page("/WaterExercise")),
            ("LoggedOn", GoalPolicy.Today.ToString("yyyy-MM-dd")), ("ExerciseId", exerciseId?.ToString() ?? ""),
            ("DurationMin", "30"), ("Intensity", intensity), ("CustomName", "Leo cầu thang"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(count, await db.ExerciseLogs.CountAsync());
    }
    [Fact] public async Task WeightPost_UpdatesBudget_PreservesDeficitAndMealHistory()
    {
        await Login(); var token = Token(await Page("/WeightLog")); var user = await UserId();
        // The QA user's most recent entry is 71.7 kg on the same day, so the weight change needs confirmation.
        var response = await Post("/WeightLog/Save", token, ("WeightKg", "69"),
            ("LoggedOn", GoalPolicy.Today.ToString("yyyy-MM-dd")), ("ConfirmLargeWeightChange", "true"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.SingleAsync(g => g.UserId == user && g.IsActive);
        Assert.Equal(2038, goal.DailyCalorieTarget); Assert.Equal(500m, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
        Assert.Equal(28, await db.MealLogs.CountAsync(m => m.UserId == user));
    }

    private Task<HttpResponseMessage> PostProfileWeight(string token, string weight, bool confirmed = false) =>
        Post("/Profile", token, ("FullName", "Người kiểm thử cân nặng"), ("Gender", "Male"),
            ("DateOfBirth", GoalPolicy.Today.AddYears(-24).ToString("yyyy-MM-dd")), ("HeightCm", "170"),
            ("CurrentWeightKg", weight), ("TargetWeightKg", "65"), ("ActivityLevel", "Moderate"),
            ("DietType", "Normal"), ("DailyWaterGoalMl", "2000"),
            ("ConfirmLargeWeightChange", confirmed ? "true" : "false"));

    private static IQueryable<WeightLog> UserWeightLogs(AppDbContext db, int userId) =>
        db.WeightLogs.Where(log => log.UserId == userId).OrderByDescending(log => log.LoggedOn).ThenByDescending(log => log.Id);

    private async Task SeedWeightHistory(int userId, decimal profileWeight, int latestDaysAgo = 1, decimal logWeight = 70m)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.CurrentWeightKg = profileWeight;
        var logs = await UserWeightLogs(db, userId).ToListAsync();
        foreach (var log in logs)
            log.LoggedOn = log.LoggedOn.AddDays(-latestDaysAgo);
        logs[0].WeightKg = logWeight;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ProfileWeight_CreatesAndUpsertsTodaysLogWithoutWritingAnUnchangedWeight()
    {
        await Login();
        var user = await UserId();
        await SeedWeightHistory(user, 70m);
        var token = Token(await Page("/Profile"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var todaysLogs = UserWeightLogs(db, user).AsNoTracking().Where(log => log.LoggedOn == GoalPolicy.Today);

        using (var unchanged = await PostProfileWeight(token, "70"))
            Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Empty(await todaysLogs.ToListAsync());

        using (var first = await PostProfileWeight(token, "71"))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var initial = await todaysLogs.SingleAsync();
        Assert.Equal(71m, initial.WeightKg);
        Assert.Equal("Cập nhật từ Hồ sơ", initial.Note);

        using (var second = await PostProfileWeight(token, "71.5"))
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var updated = await todaysLogs.SingleAsync();
        Assert.Equal(initial.Id, updated.Id);
        Assert.Equal(71.5m, updated.WeightKg);
        Assert.Equal("Cập nhật từ Hồ sơ", updated.Note);

        using (var unchanged = await PostProfileWeight(token, "71.50"))
            Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Equal(updated.Id, (await todaysLogs.SingleAsync()).Id);
        Assert.Equal(71.5m, (await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == user)).CurrentWeightKg);
        var goal = await db.Goals.AsNoTracking().SingleAsync(g => g.UserId == user && g.IsActive);
        Assert.Equal(500m, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
        Assert.Equal(2077, goal.DailyCalorieTarget);
    }

    [Fact]
    public async Task LegacyWeightMismatch_SurvivesUnchangedProfileSubmissionAndDashboardChartUsesLog()
    {
        await Login();
        var user = await UserId();
        await SeedWeightHistory(user, 85m);
        foreach (var path in new[] { "/Dashboard", "/Profile", "/WeightLog" })
        {
            var html = WebUtility.HtmlDecode(await Page(path));
            Assert.Contains("khác bản ghi cân gần nhất", html);
            Assert.Contains("/WeightLog/ReconcileUseProfile", html);
            Assert.Contains("/WeightLog/ReconcileUseLog", html);
        }

        using (var response = await PostProfileWeight(Token(await Page("/Profile")), "85"))
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(85m, (await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == user)).CurrentWeightKg);
        var logs = await UserWeightLogs(db, user).AsNoTracking().ToListAsync();
        Assert.Equal(28, logs.Count);
        Assert.Equal(70m, logs[0].WeightKg);
        var view = Assert.IsType<ViewResult>(await ControllerFor<DashboardController>(scope.ServiceProvider, user).Index());
        var model = Assert.IsType<DashboardViewModel>(view.Model);
        Assert.Equal(logs.AsEnumerable().Reverse().Select(log => log.WeightKg), model.WeightChartValues);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LargeWeightChange_RequiresConfirmationBeforeEitherFormSaves(bool fromProfile)
    {
        await Login();
        var user = await UserId();
        await SeedWeightHistory(user, 70m, latestDaysAgo: 14);
        var page = fromProfile ? "/Profile" : "/WeightLog";
        var token = Token(await Page(page));
        Task<HttpResponseMessage> Submit(string antiforgery, bool confirmed) => fromProfile
            ? PostProfileWeight(antiforgery, "85", confirmed)
            : Post("/WeightLog/Save", antiforgery, ("Form.WeightKg", "85"),
                ("Form.LoggedOn", GoalPolicy.Today.ToString("yyyy-MM-dd")),
                ("Form.ConfirmLargeWeightChange", confirmed ? "true" : "false"));

        using var rejected = await Submit(token, false);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
        Assert.Contains("Cân nặng thay đổi", html);
        Assert.Contains("ConfirmLargeWeightChange", html);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profiles = db.UserProfiles.AsNoTracking().Where(p => p.UserId == user);
        var userAccounts = db.Users.AsNoTracking().Where(a => a.Id == user);
        Assert.Equal(70m, (await profiles.SingleAsync()).CurrentWeightKg);
        Assert.Equal("Kiểm thử NutriTrackAI", (await userAccounts.SingleAsync()).FullName);
        Assert.Equal(28, await UserWeightLogs(db, user).CountAsync());

        using var accepted = await Submit(Token(html), true);
        Assert.Equal(fromProfile ? HttpStatusCode.OK : HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal(85m, (await profiles.SingleAsync()).CurrentWeightKg);
        var latest = await UserWeightLogs(db, user).AsNoTracking().FirstAsync();
        Assert.Equal(85m, latest.WeightKg);
        Assert.Equal(GoalPolicy.Today, latest.LoggedOn);
    }

    [Theory]
    [InlineData(14, "72.1", 70)] // Exactly 3% at the window boundary.
    [InlineData(14, "52", 50)] // Exactly 2 kg when the percent threshold is lower.
    [InlineData(15, "85", 70)] // Outside the window, even for a large difference.
    public async Task WeightPost_SavesWithoutConfirmationAtThresholdOrOutsideWindow(int days, string weight, int previous)
    {
        await Login();
        var user = await UserId();
        await SeedWeightHistory(user, previous, latestDaysAgo: days, logWeight: previous);
        using var response = await Post("/WeightLog/Save", Token(await Page("/WeightLog")),
            ("WeightKg", weight), ("LoggedOn", GoalPolicy.Today.ToString("yyyy-MM-dd")));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expected = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expected, (await UserWeightLogs(db, user).FirstAsync()).WeightKg);
        Assert.Equal(expected, (await db.UserProfiles.SingleAsync(p => p.UserId == user)).CurrentWeightKg);
    }

    [Theory]
    [InlineData("ReconcileUseProfile", 85, 29, 2286)]
    [InlineData("ReconcileUseLog", 70, 28, 2054)]
    public async Task Reconcile_RequiresValidSnapshotAndTokenAndOnlyChangesCurrentUser(
        string action, int expectedWeight, int expectedLogCount, int expectedCalories)
    {
        await Login();
        var user = await UserId();
        var other = await UserId("qa-b@example.test");
        await SeedWeightHistory(user, 85m);
        await SeedWeightHistory(other, 89m, logWeight: 69m);
        var route = $"/WeightLog/{action}";
        var html = await Page("/Profile");
        var token = Token(html);
        var date = GoalPolicy.Today.AddDays(-1).ToString("yyyy-MM-dd");
        (string Key, string Value)[] Fields(string profile, string log) =>
            [("expectedProfileWeight", profile), ("expectedLogWeight", log), ("expectedLogDate", date),
             ("returnTo", "Profile"), ("userId", other.ToString())];
        using var noToken = await client.PostAsync(route, new FormUrlEncodedContent(Fields("85", "70")
            .Select(field => new KeyValuePair<string, string>(field.Key, field.Value))));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profiles = db.UserProfiles.AsNoTracking();
        var ownLogs = UserWeightLogs(db, user).AsNoTracking();
        using var stale = await Post(route, token, Fields("89", "69"));
        Assert.Contains(stale.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.BadRequest, HttpStatusCode.Conflict });
        Assert.Equal(85m, (await profiles.SingleAsync(p => p.UserId == user)).CurrentWeightKg);
        Assert.Equal(28, await ownLogs.CountAsync());

        using var accepted = await Post(route, token, Fields("85", "70"));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("/Profile", accepted.Headers.Location!.ToString());
        Assert.Equal((decimal)expectedWeight, (await profiles.SingleAsync(p => p.UserId == user)).CurrentWeightKg);
        Assert.Equal(expectedLogCount, await ownLogs.CountAsync());
        var latest = await ownLogs.FirstAsync();
        Assert.Equal((decimal)expectedWeight, latest.WeightKg);
        Assert.Equal(action == "ReconcileUseProfile" ? GoalPolicy.Today : GoalPolicy.Today.AddDays(-1), latest.LoggedOn);
        Assert.Equal(expectedCalories, (await db.Goals.AsNoTracking().SingleAsync(g => g.UserId == user && g.IsActive)).DailyCalorieTarget);
        Assert.Equal(89m, (await profiles.SingleAsync(p => p.UserId == other)).CurrentWeightKg);
        var otherLogs = await UserWeightLogs(db, other).AsNoTracking().ToListAsync();
        Assert.Equal(28, otherLogs.Count);
        Assert.Equal(69m, otherLogs[0].WeightKg);
    }

    [Fact]
    public async Task MovingThenDeletingLatestWeight_RecalculatesProfileFromLatestRemainingLog()
    {
        await Login();
        var user = await UserId();
        await SeedWeightHistory(user, 70m);
        var token = Token(await Page("/WeightLog"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await UserWeightLogs(db, user).AsNoTracking().ToListAsync();
        var profiles = db.UserProfiles.AsNoTracking().Where(p => p.UserId == user);
        var olderDate = GoalPolicy.Today.AddDays(-35);

        using var collision = await Post("/WeightLog/Save", token, ("Form.Id", logs[0].Id.ToString()),
            ("Form.WeightKg", "70"), ("Form.LoggedOn", logs[1].LoggedOn.ToString("yyyy-MM-dd")));
        Assert.Equal(HttpStatusCode.OK, collision.StatusCode);
        Assert.Contains("Ngày này đã có bản ghi cân nặng", WebUtility.HtmlDecode(await collision.Content.ReadAsStringAsync()));
        Assert.Equal(logs[0].LoggedOn, (await db.WeightLogs.AsNoTracking().SingleAsync(log => log.Id == logs[0].Id)).LoggedOn);

        using var moved = await Post("/WeightLog/Save", token, ("Form.Id", logs[0].Id.ToString()),
            ("Form.WeightKg", "70"), ("Form.LoggedOn", olderDate.ToString("yyyy-MM-dd")));
        Assert.Equal(HttpStatusCode.Redirect, moved.StatusCode);
        Assert.Equal(olderDate, (await db.WeightLogs.AsNoTracking().SingleAsync(log => log.Id == logs[0].Id)).LoggedOn);
        Assert.Equal(logs[1].WeightKg, (await profiles.SingleAsync()).CurrentWeightKg);

        using var deleted = await Post("/WeightLog/Delete", token, ("id", logs[1].Id.ToString()));
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.False(await db.WeightLogs.AnyAsync(log => log.Id == logs[1].Id));
        Assert.Equal(logs[2].WeightKg, (await profiles.SingleAsync()).CurrentWeightKg);
        Assert.DoesNotContain("khác bản ghi cân gần nhất", WebUtility.HtmlDecode(await Page("/WeightLog")));
    }

    [Fact]
    public async Task DashboardWithOnlyOlderWeightLogs_ShowsTheLatestActualWeightAndDate()
    {
        var user = await UserId();
        await SeedWeightHistory(user, 85m, latestDaysAgo: 31);
        using var scope = app.Services.CreateScope();
        var view = Assert.IsType<ViewResult>(await ControllerFor<DashboardController>(scope.ServiceProvider, user).Index());
        var model = Assert.IsType<DashboardViewModel>(view.Model);

        Assert.Equal(70m, model.CurrentWeightKg);
        Assert.Equal(70m, Assert.Single(model.WeightChartValues));
        Assert.Equal(GoalPolicy.Today.AddDays(-31).ToString("dd/MM"), Assert.Single(model.WeightChartLabels));
    }

    [Fact]
    public async Task Dashboard_SuspendedPlanExplainsRestrictionOnceAndUsesVietnameseLabels()
    {
        await Login();
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        const string privateHealthNote = "Ghi chú sức khỏe riêng của hồ sơ kiểm thử";
        profile.DietType = DietType.Normal;
        profile.HealthNotes = privateHealthNote;
        profile.HealthScreeningJson = HealthScreening.Update(null,
            QaApplication.NoReportedFactors with { EatingDisorder = ScreeningAnswer.Yes });
        await RemoveTodaysMealLogs(db, user);
        await db.SaveChangesAsync();

        var html = WebUtility.HtmlDecode(await Page("/Dashboard"));
        Assert.Single(Regex.Matches(html, "Hãy hoàn thành sàng lọc sức khỏe"));
        Assert.Contains("Còn lại so với mốc duy trì (TDEE)", html);
        Assert.Contains("Mốc duy trì (TDEE)", html);
        Assert.Contains("Gợi ý món tạm ngừng vì kế hoạch calo đang tạm ngừng", html);
        Assert.Contains("Xem đánh giá kế hoạch", html);
        Assert.Contains("Giảm cân (kế hoạch calo tạm ngừng)", html);
        Assert.Contains("Cân bằng", html);
        Assert.Contains("Cảnh báo y tế quan trọng", html);
        foreach (var label in new[] { "Chất đạm", "Tinh bột", "Chất béo", "Chất xơ", "Đường" })
            Assert.Contains(label, html);
        foreach (var misleading in new[] { "−0", "Medical Disclaimer", "Carbohydrates", "Protein (Chất đạm)",
                     "Fat (Chất béo)", ">Normal<", "Chưa có món gợi ý phù hợp", privateHealthNote })
            Assert.DoesNotContain(misleading, html);
    }

    [Theory]
    [InlineData("active", "Ngân sách calo", "2.286")]
    [InlineData("suspended", "Mốc duy trì (TDEE)", "2.786")]
    [InlineData("no goal", "Mốc duy trì (TDEE)", "2.786")]
    public async Task MealLog_UsesTheEffectiveCalorieLabelAndFormatsPositiveRemainder(
        string state, string expectedLabel, string expectedCalories)
    {
        await Login();
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        profile.CurrentWeightKg = 85m;
        if (state == "suspended")
            profile.HealthScreeningJson = HealthScreening.Update(null,
                QaApplication.NoReportedFactors with { ConditionOrTreatment = ScreeningAnswer.Yes });
        if (state == "no goal")
            (await db.Goals.SingleAsync(g => g.UserId == user && g.IsActive)).IsActive = false;
        await RemoveTodaysMealLogs(db, user);
        await db.SaveChangesAsync();

        var html = WebUtility.HtmlDecode(await Page("/MealLog"));
        Assert.Contains(expectedLabel, html);
        Assert.Contains($"Còn {expectedCalories} kcal", html);
        Assert.DoesNotContain($"+{expectedCalories}", html);
        foreach (var label in new[] { "Chất đạm", "Tinh bột", "Chất béo", "Chất xơ", "Đường" })
            Assert.Contains(label, html);
        foreach (var previousLabel in new[] { "Carbohydrates", "Protein (Chất đạm)", "Fat (Chất béo)", "Chất xơ (Fiber)", "Đường (Sugar)" })
            Assert.DoesNotContain(previousLabel, html);

        var prompt = await scope.ServiceProvider.GetRequiredService<IUserContextBuilder>().BuildSystemPromptContextAsync(user);
        Assert.Contains(expectedLabel, prompt, StringComparison.OrdinalIgnoreCase);
        if (state == "suspended")
        {
            Assert.Contains("chỉ hỗ trợ theo dõi", prompt);
            Assert.DoesNotContain("CALO CÒN LẠI HÔM NAY", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Loại mục tiêu:", prompt);
        }
        if (state == "no goal")
            Assert.DoesNotContain("Loại mục tiêu:", prompt);
    }

    private static async Task RemoveTodaysMealLogs(AppDbContext db, int userId)
    {
        var todaysMeals = await db.MealLogs.Include(m => m.Details)
            .Where(m => m.UserId == userId && m.LoggedOn == GoalPolicy.Today).ToListAsync();
        db.MealLogs.RemoveRange(todaysMeals);
    }

    [Theory]
    [InlineData(MedicalConditions.Hypertension, true)]
    [InlineData(MedicalConditions.Gastritis, false)]
    public async Task SodiumProgressUsesTheScreenedProfileAndJournalOnBothPages(MedicalConditions condition, bool showLimit)
    {
        await Login();
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        profile.MedicalConditions = condition;
        profile.DoctorAwareOfPlan = true;
        profile.HealthScreeningAt = GoalPolicy.Now;
        await db.SaveChangesAsync();
        foreach (var route in new[] { "/Dashboard", "/MealLog" })
        {
            var html = WebUtility.HtmlDecode(await Page(route));
            Assert.Equal(showLimit, html.Contains("data-sodium-limit=\"2000\""));
            if (showLimit) Assert.Contains("Natri hôm nay", html);
        }
    }

    [Fact]
    public async Task GeneratedMenuUsesFourMealsForGastritisEvenWhenThreeWereRequested()
    {
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        profile.MedicalConditions = MedicalConditions.Gastritis;
        profile.HealthScreeningAt = GoalPolicy.Now;
        await db.SaveChangesAsync();
        var planner = scope.ServiceProvider.GetRequiredService<IMealPlannerService>();
        var plan = await planner.GeneratePlanAsync(user, numberOfMeals: 3);
        Assert.Equal(4, plan.Meals.Count);
        Assert.All(plan.Meals, meal => Assert.NotEmpty(meal.Items));
        Assert.NotNull(plan.RecommendedMacros);
    }

    [Fact]
    public async Task LowSodiumMenuPrefersTheLowestSodiumFoodInTheChosenCategory()
    {
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        profile.MedicalConditions = MedicalConditions.Hypertension;
        profile.DoctorAwareOfPlan = true;
        profile.HealthScreeningAt = GoalPolicy.Now;
        var goal = await db.Goals.SingleAsync(g => g.UserId == user && g.IsActive);
        goal.CaloriePlanJson = new CaloriePlanState { Adjustment = 300 }.ToJson();
        var candidates = await db.Foods.Where(food => food.CategoryId == 1 && !food.IsHidden).OrderBy(food => food.Id).ToListAsync();
        Assert.True(candidates.Count >= 2);
        candidates[0].Sodium = 10;
        foreach (var food in candidates.Skip(1)) food.Sodium = 800;
        await db.SaveChangesAsync();
        var plan = await scope.ServiceProvider.GetRequiredService<IMealPlannerService>().GeneratePlanAsync(user);
        foreach (var type in new[] { MealType.Lunch, MealType.Dinner })
        {
            var meal = Assert.Single(plan.Meals, meal => meal.MealType == type);
            var main = Assert.Single(meal.Items, food => food.CategoryId == 1);
            Assert.Equal(candidates[0].Id, main.FoodId);
        }
    }

    [Fact]
    public async Task WeightHistory_PaginatesRowsAndKeepsFullChartAndTotalChange()
    {
        await Login();
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.WeightLogs.AddRange(Enumerable.Range(28, 15).Select(daysAgo => new WeightLog
        {
            UserId = user, LoggedOn = GoalPolicy.Today.AddDays(-daysAgo), WeightKg = 73m
        }));
        await db.SaveChangesAsync();
        var history = await UserWeightLogs(db, user).ToListAsync();
        Assert.Equal(43, history.Count);
        var fullChart = history.AsEnumerable().Reverse().Select(log => log.WeightKg);

        for (var page = 1; page <= 3; page++)
        {
            var path = page == 1 ? "/WeightLog" : $"/WeightLog?page={page}";
            var html = WebUtility.HtmlDecode(await Page(path));
            var ids = Regex.Matches(html, "data-log-id=\"(\\d+)\"").Select(match => int.Parse(match.Groups[1].Value));
            Assert.Equal(history.Skip((page - 1) * 15).Take(15).Select(log => log.Id), ids);
            var chartJson = Regex.Match(html, @"const chartWeights = (\[[^;]*\]);").Groups[1].Value;
            Assert.Equal(fullChart, System.Text.Json.JsonSerializer.Deserialize<decimal[]>(chartJson));
            var weightChange = (history[0].WeightKg - history[^1].WeightKg).ToString("0.0");
            Assert.Matches($@"Tổng biến động</small>\s*<h3[^>]*>\s*{Regex.Escape(weightChange)} kg", html);
            Assert.DoesNotContain("Chart.js Live", html);
        }
    }

    [Theory]
    [InlineData("/MealLog", true)]
    [InlineData("/WaterExercise", true)]
    [InlineData("/WeightLog", false)]
    public async Task DiaryDateInput_HasVietnameseLabelForItsSelectedDate(string route, bool hasDayQuery)
    {
        await Login();
        var selectedDate = hasDayQuery ? GoalPolicy.Today.AddDays(-2) : GoalPolicy.Today;
        var path = hasDayQuery ? $"{route}?date={selectedDate:yyyy-MM-dd}" : route;
        var html = WebUtility.HtmlDecode(await Page(path));
        var input = Regex.Match(html, "<input\\b[^>]*data-business-date=\"([^\"]+)\"[^>]*>");
        Assert.True(input.Success, $"{route} lacks a date input label binding.");
        var id = Regex.Escape(input.Groups[1].Value);
        var dateText = selectedDate.ToString("dddd, dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
        Assert.Matches(new Regex($"<[^>]+id=\"{id}\"[^>]*>\\s*{Regex.Escape(dateText)}\\s*<", RegexOptions.IgnoreCase), html);
    }

    [Theory] [InlineData("Lose", 65)] [InlineData("Maintain", 72)]
    public async Task RegisterLoginAndOnboarding_RemainUsable(string goalType, int expectedWeight)
    {
        var token = Token(await Page("/Account/Register"));
        var response = await Post("/Account/Register", token, ("FullName", "Người dùng kiểm thử"), ("Email", "qa-new@example.test"),
            ("Password", "QaPass123!"), ("ConfirmPassword", "QaPass123!"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var onboarding = await Page("/Onboarding");
        response = await Post("/Onboarding/Complete", Token(onboarding), ("Gender", "Male"), ("DateOfBirth", GoalPolicy.Today.AddYears(-24).ToString("yyyy-MM-dd")),
            ("HeightCm", "170"), ("CurrentWeightKg", "72"), ("TargetWeightKg", "65"), ("GoalType", goalType), ("DurationWeeks", "16"),
            ("ActivityLevel", "Moderate"), ("DietType", "Normal"), ("DailyWaterGoalMl", "2000"),
            ("Screening.NoConditions", "true"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await Page("/Dashboard"); await Page("/MealLog");
        var user = await UserId("qa-new@example.test"); using var scope = app.Services.CreateScope();
        var goal = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Goals.SingleAsync(g => g.UserId == user && g.IsActive);
        Assert.Equal((decimal)expectedWeight, goal.TargetWeight);
        var profile = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserProfiles.SingleAsync(p => p.UserId == user);
        Assert.Equal(MedicalConditions.None, profile.MedicalConditions);
        Assert.InRange(profile.HealthScreeningAt!.Value, GoalPolicy.Now.AddMinutes(-2), GoalPolicy.Now);
    }

    [Fact]
    public async Task OnboardingPace_PreviewMatchesTheAcknowledgedPlanAndRejectsMissingAcknowledgment()
    {
        const string email = "qa-pace@example.test";
        var registerToken = Token(await Page("/Account/Register"));
        using var register = await Post("/Account/Register", registerToken, ("FullName", "Người dùng kiểm thử"), ("Email", email),
            ("Password", "QaPass123!"), ("ConfirmPassword", "QaPass123!"));
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);

        var token = Token(await Page("/Onboarding"));
        var input = new
        {
            Gender = "Male", DateOfBirth = GoalPolicy.Today.AddYears(-35).ToString("yyyy-MM-dd"),
            HeightCm = 175, CurrentWeightKg = 85, TargetWeightKg = 75, GoalType = "Lose", DurationWeeks = 13,
            ActivityLevel = "Light", DietType = "Normal", Screening = new HealthScreeningForm { NoConditions = true }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Onboarding/CalculatePreview");
        request.Headers.Add("RequestVerificationToken", token);
        request.Content = new StringContent(JsonSerializer.Serialize(input), Encoding.UTF8, "application/json");
        using var preview = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        var plan = json.RootElement;
        Assert.False(plan.GetProperty("isBlocked").GetBoolean());
        Assert.Equal("Intense", plan.GetProperty("paceZone").GetString());
        Assert.False(string.IsNullOrWhiteSpace(plan.GetProperty("paceZoneLabel").GetString()));
        Assert.True(plan.GetProperty("requiresIntensePaceAck").GetBoolean());
        Assert.Equal(.78m, plan.GetProperty("maxWeeklyRateKg").GetDecimal());
        Assert.InRange(plan.GetProperty("appliedWeeklyRateKg").GetDecimal(), .76m, .77m);
        Assert.InRange(plan.GetProperty("plannedDailyAdjustment").GetDecimal(), 846.1538m, 846.1539m);
        Assert.Equal(1593, plan.GetProperty("dailyCalorieTarget").GetInt32());
        Assert.Contains(plan.GetProperty("paceOptions").EnumerateArray(), option =>
            option.GetProperty("key").GetString() == "fastest" && option.GetProperty("weeks").GetInt32() == 13);

        (string Key, string Value)[] Form(bool acknowledge) =>
        [
            ("Gender", "Male"), ("DateOfBirth", input.DateOfBirth), ("HeightCm", "175"), ("CurrentWeightKg", "85"),
            ("TargetWeightKg", "75"), ("GoalType", "Lose"), ("DurationWeeks", "13"), ("ActivityLevel", "Light"),
            ("DietType", "Normal"), ("DailyWaterGoalMl", "2000"), ("AcknowledgeIntensePace", acknowledge ? "true" : "false"),
            ("Screening.NoConditions", "true")
        ];
        var user = await UserId(email);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goals = db.Goals.AsNoTracking().Where(goal => goal.UserId == user);

        using var missingAck = await Post("/Onboarding/Complete", token, Form(false));
        Assert.Equal(HttpStatusCode.OK, missingAck.StatusCode);
        var deniedHtml = await missingAck.Content.ReadAsStringAsync();
        Assert.Contains("field-validation-error", deniedHtml);
        Assert.Contains("data-valmsg-for=\"AcknowledgeIntensePace\"", deniedHtml);
        Assert.Contains(GoalPolicy.IntensePaceAcknowledgmentMessage, WebUtility.HtmlDecode(deniedHtml));
        Assert.Empty(await goals.ToListAsync());
        Assert.False(await db.WeightLogs.AnyAsync(log => log.UserId == user));

        using var accepted = await Post("/Onboarding/Complete", token, Form(true));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var saved = await goals.SingleAsync();
        Assert.True(saved.IsActive);
        Assert.Equal(plan.GetProperty("dailyCalorieTarget").GetInt32(), saved.DailyCalorieTarget);
        Assert.Equal(plan.GetProperty("plannedDailyAdjustment").GetDecimal(), CaloriePlanState.Read(saved.CaloriePlanJson)!.Adjustment);
        Assert.Equal(GoalPolicy.Today.AddDays(91), saved.TargetDate);
    }

    [Fact]
    public async Task Onboarding_RequiresScreeningAndAllowsTrackingWhenBlocked()
    {
        var token = Token(await Page("/Account/Register"));
        var register = await Post("/Account/Register", token, ("FullName", "Người kiểm thử"), ("Email", "qa-unscreened@example.test"),
            ("Password", "QaPass123!"), ("ConfirmPassword", "QaPass123!"));
        Assert.Equal(HttpStatusCode.Redirect, register.StatusCode);
        var onboarding = await Page("/Onboarding");
        Assert.Contains("name=\"Screening.Conditions\"", onboarding);
        var fields = new (string, string)[]
        {
            ("Gender", "Male"), ("DateOfBirth", GoalPolicy.Today.AddYears(-24).ToString("yyyy-MM-dd")),
            ("HeightCm", "170"), ("CurrentWeightKg", "72"), ("TargetWeightKg", "65"), ("GoalType", "Lose"),
            ("DurationWeeks", "16"), ("ActivityLevel", "Moderate"), ("DietType", "Normal")
        };
        var forged = await Post("/Onboarding/Complete", Token(onboarding),
            [.. fields, ("HealthScreeningAt", GoalPolicy.Now.ToString("O")), ("MedicalConditions", "None"),
             ("HealthScreeningJson", HealthScreening.Update(null, QaApplication.NoReportedFactors)!)]);
        Assert.Equal(HttpStatusCode.OK, forged.StatusCode);
        Assert.Contains(GoalPolicy.CompleteHealthScreening, WebUtility.HtmlDecode(await forged.Content.ReadAsStringAsync()));

        var complete = await Post("/Onboarding/Complete", Token(onboarding), [..fields, ("Screening.Conditions", "KidneyDisease")]);
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        var dashboard = WebUtility.HtmlDecode(await Page("/Dashboard"));
        Assert.Contains("Kế hoạch calo đang tạm ngừng", dashboard);
        Assert.DoesNotContain("Bệnh thận mạn", dashboard);
        await Page("/MealLog");
        var id = await UserId("qa-unscreened@example.test");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(MedicalConditions.KidneyDisease, (await db.UserProfiles.SingleAsync(p => p.UserId == id)).MedicalConditions);
        Assert.False(await db.Goals.AnyAsync(g => g.UserId == id && g.IsActive));
        var date = GoalPolicy.Today.ToString("yyyy-MM-dd");
        var weight = await Post("/WeightLog/Save", Token(await Page("/WeightLog")),
            ("WeightKg", "71.5"), ("LoggedOn", date));
        var food = await Post("/MealLog/AddFood", Token(await Page("/MealLog")),
            ("FoodId", "1"), ("Servings", "1"), ("MealType", "1"), ("LoggedOn", date));
        var water = await Post("/Dashboard/QuickAddWater", Token(await Page("/Dashboard")), ("amountMl", "250"));
        Assert.All(new[] { weight, food, water }, response => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode));
        Assert.True(await db.WeightLogs.AnyAsync(w => w.UserId == id && w.WeightKg == 71.5m));
        Assert.True(await db.MealLogDetails.AnyAsync(m => m.MealLog.UserId == id && m.FoodId == 1));
        Assert.True(await db.WaterLogs.AnyAsync(w => w.UserId == id && w.AmountMl == 250));
        var create = await Post("/Goal/Create", Token(await Page("/Goal/Create")), ("GoalType", "Lose"),
            ("TargetWeight", "65"), ("DurationWeeks", "16"), ("Screening.NoConditions", "true"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.False(await db.Goals.AnyAsync(g => g.UserId == id && g.IsActive));
    }

    [Fact]
    public async Task ProfileScreening_RestrictsActivePlanAndDoesNotRestoreItOrModifyOtherUsers()
    {
        await Login();
        var id = await UserId();
        var otherId = await UserId("qa-b@example.test");
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var originalOther = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == otherId);

        async Task Save((string, string) answer)
        {
            var html = await Page("/Profile");
            Assert.Contains("name=\"Screening.Conditions\"", html);
            var response = await Post("/Profile", Token(html), ("FullName", "Người kiểm thử"), ("UserId", otherId.ToString()),
                ("Gender", "Male"), ("DateOfBirth", GoalPolicy.Today.AddYears(-24).ToString("yyyy-MM-dd")),
                ("HeightCm", "170"), ("CurrentWeightKg", "71.7"), ("TargetWeightKg", "65"), ("ActivityLevel", "Moderate"),
                ("DietType", "Normal"), ("DailyWaterGoalMl", "2000"), answer);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        await Save(("Screening.Conditions", "Hypertension"));
        var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == id);
        Assert.Equal(MedicalConditions.Hypertension, profile.MedicalConditions);
        Assert.Equal(HealthTier.NeedsDoctor, GoalPolicy.EvaluateHealth(profile).Tier);
        var goal = await db.Goals.AsNoTracking().SingleAsync(g => g.UserId == id && g.IsActive);
        var body = await Post("/Goal/BodyCheckIn", Token(await Page("/Goal")), ("id", goal.Id.ToString()), ("feedback", "Increase"));
        Assert.Equal(HttpStatusCode.Redirect, body.StatusCode);
        await Save(("Screening.NoConditions", "true"));
        var updated = await db.Goals.AsNoTracking().SingleAsync(g => g.Id == goal.Id);
        var state = CaloriePlanState.Read(updated.CaloriePlanJson)!;
        Assert.True(state.RequiresPlanReconfirmation);
        Assert.Equal(500m, state.Adjustment);
        Assert.Equal(2580, updated.DailyCalorieTarget);
        var other = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == otherId);
        Assert.Equal(originalOther.MedicalConditions, other.MedicalConditions);
        Assert.Equal(originalOther.HealthScreeningAt, other.HealthScreeningAt);
        var chat = await scope.ServiceProvider.GetRequiredService<IUserContextBuilder>().BuildSystemPromptContextAsync(id);
        Assert.Contains("chỉ hỗ trợ theo dõi", chat);
        Assert.DoesNotContain(nameof(HealthScreeningAnswers.ConditionOrTreatment), chat);
    }
    [Fact] public async Task ChatbotFallback_ExplainsServicePlanWithoutChangingIt()
    {
        var user = await UserId(); using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider; var db = services.GetRequiredService<AppDbContext>();
        var before = (await db.Goals.SingleAsync(g => g.UserId == user)).CaloriePlanJson;
        var response = await services.GetRequiredService<IGeminiChatService>().SendMessageAsync(user, null, "Tôi đang ở giai đoạn nào, TDEE là bao nhiêu?");
        Assert.Contains("500", response.BotResponse); Assert.Contains("TDEE", response.BotResponse);
        Assert.Contains("không tự", response.BotResponse); Assert.EndsWith(GoalPolicy.MedicalDisclaimer, response.BotResponse);
        Assert.Equal(before, (await db.Goals.SingleAsync(g => g.UserId == user)).CaloriePlanJson);
    }

    [Fact]
    public async Task FoodPagesDisplayTheMatchingGeneratedPhotoAndServeIt()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await FoodSeedData.UpsertAsync(db);
        var food = await db.Foods.SingleAsync(f => f.Name == "Phở bò tái");
        using var catalogue = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(QaApplication.Root, "Data", "Seed", "FoodImages", "catalogue.json")));
        var entry = catalogue.RootElement.EnumerateArray().Single(item => item.GetProperty("Name").GetString() == food.Name);
        var imageUrl = "/uploads/foods/generated/" + entry.GetProperty("FileName").GetString();
        var index = await Page("/Food?search=" + Uri.EscapeDataString(food.Name));
        var details = await Page("/Food/Details/" + food.Id);
        foreach (var html in new[] { index, details })
        {
            Assert.Contains($"src=\"{imageUrl}\"", html);
            Assert.Contains($"alt=\"{food.Name}\"", WebUtility.HtmlDecode(html));
        }
        using var image = await client.GetAsync(imageUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.True((await image.Content.ReadAsByteArrayAsync()).Length > 10000);
    }

    [Fact]
    public async Task ChatLooksUpNewCsvDishesAndAppliesLocalProfileExclusions()
    {
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        await FoodSeedData.UpsertAsync(db);
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.Allergies = "tôm, đậu phộng";
        profile.DislikedFoods = "hành tây";
        profile.DietType = DietType.Normal;
        await db.SaveChangesAsync();

        var chat = ActivatorUtilities.CreateInstance<GeminiChatService>(services, new Microsoft.Extensions.Configuration.ConfigurationManager());
        var found = await chat.SendMessageAsync(userId, null, "Sốt Mayonnaise bao nhiêu calo?");
        Assert.Contains("lòng đỏ trứng gà", found.BotResponse);
        foreach (var name in new[] { "Bánh tráng trộn", "Mì Quảng gà", "Phở bò tái" })
        {
            var filtered = await chat.SendMessageAsync(userId, null, $"{name} bao nhiêu calo?");
            Assert.Contains("Chưa có kết quả phù hợp với bộ lọc", filtered.BotResponse);
        }
    }

    [Fact]
    public async Task GeminiRequestUsesTheSharedPromptWithoutRawProfileDataOrLegacyAssistantHistory()
    {
        var userId = await UserId();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(user => user.Id == userId);
        user.FullName = "PRIVATE-NAME-TEST";
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.HealthNotes = "PRIVATE-MEDICAL-NOTE";
        profile.Allergies = "Lạc; PRIVATE-ALLERGEN-TEXT";
        profile.DislikedFoods = "PRIVATE-DISLIKE-TEXT";
        profile.HealthScreeningAt = GoalPolicy.Now;
        profile.DoctorAwareOfPlan = true;
        profile.MedicalConditions = MedicalConditions.Gastritis | MedicalConditions.Hypertension;
        var goal = await db.Goals.SingleAsync(g => g.UserId == userId && g.IsActive);
        goal.CaloriePlanJson = new CaloriePlanState { Adjustment = 250, LastReviewedOn = GoalPolicy.Today }.ToJson();
        var history = new ChatSession { UserId = userId, Title = "Phiên kiểm thử", CreatedAt = DateTime.UtcNow };
        db.ChatSessions.Add(history);
        await db.SaveChangesAsync();
        db.ChatMessages.AddRange(
            new ChatMessage { SessionId = history.Id, Role = ChatRole.User, Content = "OLD-USER-TURN", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new ChatMessage { SessionId = history.Id, Role = ChatRole.Assistant, Content = $"OLD-ASSISTANT-TURN {user.FullName}", CreatedAt = DateTime.UtcNow.AddMinutes(-1) });
        await db.SaveChangesAsync();

        using var transport = new CapturingGeminiTransport();
        var config = new Microsoft.Extensions.Configuration.ConfigurationManager();
        config["Gemini:ApiKey"] = "qa-fake-key";
        var gemini = ActivatorUtilities.CreateInstance<GeminiChatService>(services, transport, config);
        var result = await gemini.SendMessageAsync(userId, history.Id, "Hôm nay ngân sách calo còn lại là bao nhiêu?");

        using var payload = JsonDocument.Parse(Assert.IsType<string>(transport.RequestBody));
        var instruction = payload.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString();
        Assert.Contains("RÀNG BUỘC ĂN UỐNG BẮT BUỘC", instruction);
        Assert.Contains("tránh đồ chua cay", instruction);
        Assert.Contains("ít natri", instruction);
        Assert.Contains("đậu phộng", instruction);
        Assert.Contains("dị ứng chưa xác định được nhóm", instruction);
        Assert.Contains("Ngân sách calo", instruction);
        Assert.Contains("Mốc duy trì (TDEE)", instruction);
        Assert.DoesNotContain("Cao huyết áp", instruction);
        Assert.DoesNotContain("Dạ dày (viêm loét", instruction);
        Assert.Single(instruction!.Split(GoalPolicy.MedicalDisclaimer).Skip(1));
        foreach (var privateText in new[] { user.FullName, user.Email!, profile.HealthNotes!, profile.DislikedFoods!,
                     "PRIVATE-ALLERGEN-TEXT", "OLD-USER-TURN", "OLD-ASSISTANT-TURN" })
            Assert.DoesNotContain(privateText, transport.RequestBody!);
        Assert.Single(result.BotResponse.Split(GoalPolicy.MedicalDisclaimer).Skip(1));
        Assert.EndsWith(GoalPolicy.MedicalDisclaimer, result.BotResponse);

        profile.MedicalConditions = MedicalConditions.KidneyDisease;
        await db.SaveChangesAsync();
        var tracking = await gemini.SendMessageAsync(userId, history.Id, "Tôi muốn biết ngân sách calo.");
        Assert.Contains("hỗ trợ theo dõi", tracking.BotResponse);
        Assert.Equal(1, transport.Calls);
    }

    private sealed class CapturingGeminiTransport : HttpMessageHandler, IHttpClientFactory
    {
        public string? RequestBody { get; private set; }
        public int Calls { get; private set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            var reply = $"Phản hồi giả lập.\n{GoalPolicy.MedicalDisclaimer}\n{GoalPolicy.MedicalDisclaimer}";
            return new(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    candidates = new[] { new { content = new { parts = new[] { new { text = reply } } } } }
                })
            };
        }
    }
    [Fact] public async Task ProfileActivityUpdate_KeepsDeficitAndActiveGoalTarget()
    {
        await Login(); var token = Token(await Page("/Profile")); var user = await UserId();
        var response = await Post("/Profile", token, ("FullName", "Người dùng kiểm thử"), ("Email", "qa-a@example.test"),
            ("Gender", "Male"), ("DateOfBirth", GoalPolicy.Today.AddYears(-24).ToString("yyyy-MM-dd")), ("HeightCm", "170"),
            ("CurrentWeightKg", "72"), ("TargetWeightKg", "60"), ("ActivityLevel", "Light"), ("DietType", "Normal"), ("DailyWaterGoalMl", "2000"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var goal = await db.Goals.SingleAsync(g => g.UserId == user);
        Assert.Equal(1793, goal.DailyCalorieTarget); Assert.Equal(500m, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
        Assert.Equal(65m, (await db.UserProfiles.SingleAsync(p => p.UserId == user)).TargetWeightKg);
    }
    [Fact] public async Task MaintainDoesNotRequireDifferentTargetWeight()
    {
        await Login(); var token = Token(await Page("/Goal/Create"));
        var response = await Post("/Goal/Create", token, ("GoalType", "Maintain"), ("DurationWeeks", "1"));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var user = await UserId(); using var scope = app.Services.CreateScope();
        var goal = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Goals.SingleAsync(g => g.UserId == user && g.IsActive);
        Assert.Equal(71.7m, goal.TargetWeight); Assert.Equal(2580, goal.DailyCalorieTarget);
    }

    [Fact] public async Task QaWeightTimeline_ProfileProgressAndTrendAgree()
    {
        var user = await UserId();
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", db.Database.ProviderName);
        var weights = await db.WeightLogs.Where(w => w.UserId == user).OrderBy(w => w.LoggedOn).ToListAsync();
        Assert.Equal(28, weights.Count);
        Assert.Equal(QaWeightData.Weights.ToArray(), weights.Select(w => w.WeightKg).ToArray());
        Assert.All(weights, w => Assert.Equal(w.WeightKg, Math.Round(w.WeightKg, 1)));
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == user);
        Assert.Equal(weights.Last().WeightKg, profile.CurrentWeightKg);
        var review = await services.GetRequiredService<AdaptiveCalorieService>().ReviewAsync(user);
        Assert.Equal(14, review.WeightDays);
        Assert.Equal(71.9m, review.PeriodFirstWeight);
        Assert.Equal(71.7m, review.PeriodLastWeight);
        Assert.InRange(review.FirstWindowAverage!.Value, 71.885714m, 71.885715m);
        Assert.InRange(review.LastWindowAverage!.Value, 71.771428m, 71.771429m);
        Assert.Equal(7m, review.TrendIntervalDays);
        Assert.InRange(review.WeeklyRate!.Value, .114285m, .114286m);
        Assert.InRange(review.EndpointWeeklyRate!.Value, .107692m, .107693m);
        Assert.NotEqual(review.WeeklyRate, review.EndpointWeeklyRate);
        var view = Assert.IsType<ViewResult>(await ControllerFor<GoalController>(services, user).Index());
        var model = Assert.IsType<GoalIndexViewModel>(view.Model);
        Assert.Equal(71.7m, model.CurrentWeightKg);
        Assert.Equal(.3m, model.TotalWeightChangeAchieved);
        Assert.Equal(4, model.ProgressPercent);
        Assert.Equal(2080, model.DailyCalorieTarget);
        await Login();
        var html = WebUtility.HtmlDecode(await Page("/Goal"));
        Assert.Contains("Tốc độ đầu–cuối kỳ", html);
        Assert.Contains("hai trung bình", html);
    }
}
