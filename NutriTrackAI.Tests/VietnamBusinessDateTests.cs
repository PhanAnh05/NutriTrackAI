using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NutriTrackAI.Controllers;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

[CollectionDefinition("Vietnam business date", DisableParallelization = true)]
public class VietnamBusinessDateCollection { }

[Collection("Vietnam business date")]
public class VietnamBusinessDateTests
{
    private const int UserId = 1;
    private static readonly DateTime PreviousDate = new(2026, 11, 8);
    private static readonly DateTime BusinessDate = new(2026, 11, 9);

    [Theory]
    [InlineData(16, 59, 8, 23, 59)]
    [InlineData(17, 0, 9, 0, 0)]
    [InlineData(18, 0, 9, 1, 0)]
    public void BusinessClockChangesDateAtMidnightInVietnam(int utcHour, int utcMinute, int localDay,
        int localHour, int localMinute)
    {
        var instant = new DateTime(2026, 11, 8, utcHour, utcMinute, 0, DateTimeKind.Utc);
        using var clock = GoalPolicy.OverrideUtcNow(instant);

        Assert.Equal(new DateTime(2026, 11, localDay, localHour, localMinute, 0), GoalPolicy.Now);
        Assert.Equal(new DateTime(2026, 11, localDay), GoalPolicy.Today);
    }

    [Fact]
    public async Task DailyReadsAndQuickWaterUseTheSameVietnamDate()
    {
        using var clock = GoalPolicy.OverrideUtcNow(new DateTime(2026, 11, 8, 18, 0, 0, DateTimeKind.Utc));
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var user = new ApplicationUser { Id = UserId, FullName = "Test user", Email = "date@example.test" };
        var category = new FoodCategory { Name = "Test category", Slug = "test-category" };
        var food = new Food { Name = "Test food", Category = category, Calories = 100m };
        db.AddRange(user, category, food, new UserProfile
        {
            User = user,
            DateOfBirth = new DateTime(1990, 1, 1),
            HeightCm = 170m,
            CurrentWeightKg = 70m,
            TargetWeightKg = 70m,
            OnboardingCompleted = true
        });
        AddDailyLogs(db, food, PreviousDate, 100m, 100, 30m);
        AddDailyLogs(db, food, BusinessDate, 450m, 350, 60m);
        await db.SaveChangesAsync();

        var calculator = new NutritionCalculatorService();
        var budgets = new CalorieBudgetService(db, calculator, new GoalPlanningService(calculator));
        var dashboard = WithUser(new DashboardController(db, calculator,
            NullLogger<DashboardController>.Instance, budgets, new WeightTrackingService(db)));
        var mealLog = WithUser(new MealLogController(db, NullLogger<MealLogController>.Instance, budgets));
        var waterExercise = WithUser(new WaterExerciseController(db, NullLogger<WaterExerciseController>.Instance));
        var foodTools = new FoodToolService(db, calculator, NullLogger<FoodToolService>.Instance, budgets);

        var dashboardModel = Assert.IsType<DashboardViewModel>(Assert.IsType<ViewResult>(await dashboard.Index()).Model);
        Assert.Equal(450m, dashboardModel.CaloriesConsumedToday);
        Assert.Equal(350, dashboardModel.TotalWaterTodayMl);
        Assert.Equal(60m, dashboardModel.CaloriesBurnedToday);

        var mealModel = Assert.IsType<MealLogIndexViewModel>(Assert.IsType<ViewResult>(await mealLog.Index(null)).Model);
        Assert.Equal(BusinessDate, mealModel.SelectedDate);
        Assert.Equal(450m, mealModel.TotalCalories);
        var futureMealModel = Assert.IsType<MealLogIndexViewModel>(
            Assert.IsType<ViewResult>(await mealLog.Index(BusinessDate.AddDays(1))).Model);
        Assert.Equal(BusinessDate, futureMealModel.SelectedDate);

        var waterModel = Assert.IsType<WaterExerciseIndexViewModel>(
            Assert.IsType<ViewResult>(await waterExercise.Index(null)).Model);
        Assert.Equal(BusinessDate, waterModel.SelectedDate);
        Assert.Equal(350, waterModel.TotalWaterMl);
        Assert.Equal(60m, waterModel.TotalCaloriesBurned);

        var nutrition = await foodTools.GetTodayNutritionAsync(UserId);
        Assert.Equal(BusinessDate, nutrition.Date);
        Assert.Equal(450m, nutrition.CaloriesConsumed);
        Assert.Equal(350, nutrition.WaterMl);
        var remaining = await foodTools.GetRemainingCaloriesAsync(UserId);
        Assert.Equal(BusinessDate, remaining.Date);
        Assert.Equal(450m, remaining.Consumed);

        dashboard.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        var result = Assert.IsType<JsonResult>(await dashboard.QuickAddWater());
        Assert.Equal(600, JsonSerializer.SerializeToElement(result.Value).GetProperty("totalWater").GetInt32());
        Assert.True(await db.WaterLogs.AnyAsync(log => log.UserId == UserId
            && log.LoggedOn == BusinessDate && log.AmountMl == 250));
        Assert.Equal(100, await db.WaterLogs.Where(log => log.LoggedOn == PreviousDate).SumAsync(log => log.AmountMl));
    }

    private static TController WithUser<TController>(TController controller) where TController : Controller
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, UserId.ToString()) }, "Test"))
            }
        };
        return controller;
    }

    private static void AddDailyLogs(AppDbContext db, Food food, DateTime date, decimal consumed, int water,
        decimal burned)
    {
        db.AddRange(new MealLog
        {
            UserId = UserId,
            LoggedOn = date,
            MealType = MealType.Lunch,
            Details = new List<MealLogDetail> { new() { Food = food, Calories = consumed, Servings = 1m } }
        }, new WaterLog { UserId = UserId, LoggedOn = date, AmountMl = water },
            new ExerciseLog { UserId = UserId, LoggedOn = date, CaloriesBurned = burned, DurationMin = 30 });
    }
}
