using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NutriTrackAI.Controllers;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;

namespace NutriTrackAI.Tests;

// A separate in-memory web host: no SQL Server connection, seeding of user data, or Gemini call.
public static class QaApplication
{
    internal static readonly HealthScreeningAnswers NoReportedFactors = new()
    {
        PregnancyOrBreastfeeding = ScreeningAnswer.No,
        ConditionOrTreatment = ScreeningAnswer.No,
        EatingDisorder = ScreeningAnswer.No,
        UnexpectedWeightLossOrSymptoms = ScreeningAnswer.No
    };

    public static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "NutriTrackAI.csproj"))) directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException();
        }
    }
    public static async Task<WebApplication> StartAsync(string url = "http://127.0.0.1:0", bool preview = false)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(GoalController).Assembly.FullName,
            ContentRootPath = Root, WebRootPath = Path.Combine(Root, "wwwroot"), EnvironmentName = "Development" });
        builder.WebHost.UseUrls(url);
        builder.Logging.ClearProviders();
        builder.Configuration["Gemini:ApiKey"] = "";
        var dbName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options => {
            options.Password.RequiredLength = 8; options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false; options.User.RequireUniqueEmail = true;
        }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        builder.Services.ConfigureApplicationCookie(o => { o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/AccessDenied"; });
        builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IEmailSender, EmailSender>();
        builder.Services.AddScoped<INutritionCalculatorService, NutritionCalculatorService>();
        builder.Services.AddScoped<GoalPlanningService>(); builder.Services.AddScoped<CalorieBudgetService>(); builder.Services.AddScoped<AdaptiveCalorieService>();
        builder.Services.AddScoped<IWeightTrackingService, WeightTrackingService>();
        builder.Services.AddScoped<IMealPlannerService, MealPlannerService>(); builder.Services.AddScoped<IUserContextBuilder, UserContextBuilder>();
        builder.Services.AddScoped<IFoodToolService, FoodToolService>(); builder.Services.AddScoped<IChatSafetyFilter, ChatSafetyFilter>();
        builder.Services.AddScoped<IRagService, RagService>(); builder.Services.AddScoped<IGeminiChatService, GeminiChatService>();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(GoalController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
        var app = builder.Build();
        app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        if (preview) app.MapGet("/qa/signin", async (Microsoft.AspNetCore.Http.HttpContext http,
            SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> userManager) => {
            var user = await userManager.FindByEmailAsync("qa-a@example.test");
            await signIn.SignInAsync(user!, false);
            var path = http.Request.Query["path"].ToString();
            return Microsoft.AspNetCore.Http.Results.Redirect(path == "/Goal" ? "/Goal" : "/Goal/Create");
        });
        await SeedAsync(app.Services);
        await app.StartAsync();
        return app;
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        await roles.CreateAsync(new IdentityRole<int>("User")); await roles.CreateAsync(new IdentityRole<int>("Admin"));
        foreach (var (email, role) in new[] { ("qa-a@example.test", "User"), ("qa-b@example.test", "User"), ("qa-admin@example.test", "Admin") })
        {
            var user = new ApplicationUser { UserName = email, Email = email, FullName = "Kiểm thử NutriTrackAI", EmailConfirmed = true, RoleName = role };
            var result = await users.CreateAsync(user, "QaPass123!");
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(",", result.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
            db.UserProfiles.Add(new UserProfile { UserId = user.Id, DateOfBirth = GoalPolicy.Today.AddYears(-24), Gender = Gender.Male,
                CurrentWeightKg = role == "User" ? QaWeightData.Weights[^1] : 72, HeightCm = 170, ActivityLevel = ActivityLevel.Moderate, TargetWeightKg = 65, OnboardingCompleted = true,
                Allergies = "đậu phộng", DislikedFoods = "khổ qua", DietType = DietType.Vegetarian,
                HealthScreeningJson = HealthScreening.Update(null, NoReportedFactors) });
            if (role == "User")
            {
                db.Goals.Add(new Goal { UserId = user.Id, GoalType = GoalType.Lose, StartWeight = 72, TargetWeight = 65,
                    StartDate = GoalPolicy.Today.AddDays(-28), TargetDate = GoalPolicy.Today.AddDays(84), WeeklyRateKg = .45m,
                    DailyCalorieTarget = 2085, CaloriePlanJson = new CaloriePlanState { Adjustment = 500, LastReviewedOn = GoalPolicy.Today.AddDays(-28) }.ToJson() });
                for (var day = 0; day < 28; day++)
                {
                    db.WeightLogs.Add(new WeightLog { UserId = user.Id, LoggedOn = GoalPolicy.Today.AddDays(day - 27), WeightKg = QaWeightData.Weights[day] });
                    db.MealLogs.Add(new MealLog { UserId = user.Id, LoggedOn = GoalPolicy.Today.AddDays(day - 27), MealType = MealType.Lunch,
                        Details = new List<MealLogDetail> { new() { FoodId = 1, Calories = 2085, Protein = 100, Carb = 250, Fat = 70, Servings = 1 } } });
                }
            }
        }
        var category = new FoodCategory { Id = 1, Name = "Món kiểm thử", Slug = FoodCategorySlugs.Main };
        db.FoodCategories.Add(category);
        db.Exercises.Add(new Exercise { Name = "Bóng đá", MetValue = 7m });
        for (var i = 1; i <= 15; i++) db.Foods.Add(new Food { Id = i, CategoryId = 1, Name = $"Món đậu hũ kiểm thử {i}",
            Calories = 300, Protein = 20, Carb = 30, Fat = 11, ServingSize = 1, ServingName = "phần", IsVegetarian = true, Ingredients = "đậu hũ, rau" });
        db.Foods.Add(new Food { Id = 20, CategoryId = 1, Name = "Món dị ứng", Calories = 300, AllergenNotes = "đậu phộng", IsVegetarian = true, ServingSize = 1 });
        db.Foods.Add(new Food { Id = 21, CategoryId = 1, Name = "Món thịt", Calories = 300, IsVegetarian = false, ServingSize = 1 });
        db.Foods.Add(new Food { Id = 22, CategoryId = 1, Name = "Khổ qua", Calories = 300, IsVegetarian = true, Ingredients = "khổ qua", ServingSize = 1 });
        db.Foods.Add(new Food { Id = 23, CategoryId = 1, Name = "Món ẩn", Calories = 300, IsVegetarian = true, IsHidden = true, ServingSize = 1 });
        await db.SaveChangesAsync();
        var budgets = scope.ServiceProvider.GetRequiredService<CalorieBudgetService>();
        foreach (var userId in await db.Goals.Select(g => g.UserId).Distinct().ToListAsync())
            await budgets.RefreshAsync(userId);
    }
}
