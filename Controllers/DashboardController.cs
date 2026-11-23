using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Filters;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

[Authorize]
[RequireOnboarding]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _nutritionCalculator;
    private readonly ILogger<DashboardController> _logger;
    private readonly CalorieBudgetService _budgets;
    private readonly IWeightTrackingService _weights;

    public DashboardController(
        AppDbContext context,
        INutritionCalculatorService nutritionCalculator,
        ILogger<DashboardController> logger, CalorieBudgetService budgets, IWeightTrackingService weights)
    {
        _context = context;
        _nutritionCalculator = nutritionCalculator;
        _logger = logger;
        _budgets = budgets;
        _weights = weights;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // 1. Chống lỗi IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userId = GetCurrentUserId();
        var today = GoalPolicy.Today;

        // 2. Lấy thông tin hồ sơ UserProfile & User
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            return RedirectToAction("Index", "Onboarding");
        }

        var latestWeight = await _weights.GetLatestAsync(userId);
        var trackedWeight = latestWeight?.WeightKg ?? profile.CurrentWeightKg;

        // Tính các chỉ số nhân trắc học
        var age = _nutritionCalculator.CalculateAge(profile.DateOfBirth);
        var bmiInfo = _nutritionCalculator.GetBmiInfo(profile.CurrentWeightKg, profile.HeightCm, _nutritionCalculator.CalculateAge(profile.DateOfBirth));
        var bmr = _nutritionCalculator.CalculateBmr(profile.Gender, profile.CurrentWeightKg, profile.HeightCm, age);
        var tdee = _nutritionCalculator.CalculateTdee(bmr, profile.ActivityLevel);

        // 3. Lấy mục tiêu đang kích hoạt (Goal active)
        var activeGoal = await _context.Goals
            .FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);

        var hasGoal = activeGoal != null;
        var goalType = activeGoal?.GoalType ?? GoalType.Maintain;
        var budget = await _budgets.GetAsync(userId);
        var dailyTarget = budget.TargetCalories;
        var startingWeight = activeGoal?.StartWeight ?? trackedWeight;
        var targetWeight = activeGoal?.TargetWeight ?? profile.TargetWeightKg;

        // Tính tiến độ mục tiêu (%)
        decimal progressPercent = 0;
        if (hasGoal)
        {
            if (goalType == GoalType.Lose)
            {
                var totalDiff = startingWeight - targetWeight;
                if (totalDiff > 0)
                {
                    var lost = startingWeight - trackedWeight;
                    progressPercent = Math.Clamp(Math.Round((lost / totalDiff) * 100m, 1), 0, 100);
                }
                else
                {
                    progressPercent = 100;
                }
            }
            else if (goalType == GoalType.Gain)
            {
                var totalDiff = targetWeight - startingWeight;
                if (totalDiff > 0)
                {
                    var gained = trackedWeight - startingWeight;
                    progressPercent = Math.Clamp(Math.Round((gained / totalDiff) * 100m, 1), 0, 100);
                }
                else
                {
                    progressPercent = 100;
                }
            }
            else // Maintain
            {
                var diff = Math.Abs(trackedWeight - targetWeight);
                progressPercent = diff <= 0.5m ? 100 : Math.Max(0, 100 - (diff * 20));
            }
        }

        var daysRemaining = activeGoal != null
            ? Math.Max(0, (activeGoal.TargetDate.Date - today).Days)
            : 0;

        // 4. Nhật ký bữa ăn hôm nay
        var mealLogs = await _context.MealLogs
            .Include(m => m.Details)
                .ThenInclude(d => d.Food)
            .Where(m => m.UserId == userId && m.LoggedOn == today)
            .ToListAsync();

        var allDetails = mealLogs.SelectMany(m => m.Details).ToList();
        var caloriesConsumed = allDetails.Sum(d => d.Calories);
        var actualProtein = allDetails.Sum(d => d.Protein);
        var actualCarb = allDetails.Sum(d => d.Carb);
        var actualFat = allDetails.Sum(d => d.Fat);
        var actualFiber = allDetails.Sum(d => d.Fiber);
        var actualSugar = allDetails.Sum(d => d.Sugar);
        var actualSodium = allDetails.Sum(d => d.Sodium);

        var breakfastCal = mealLogs.FirstOrDefault(m => m.MealType == MealType.Breakfast)?.Details.Sum(d => d.Calories) ?? 0;
        var lunchCal = mealLogs.FirstOrDefault(m => m.MealType == MealType.Lunch)?.Details.Sum(d => d.Calories) ?? 0;
        var dinnerCal = mealLogs.FirstOrDefault(m => m.MealType == MealType.Dinner)?.Details.Sum(d => d.Calories) ?? 0;
        var snackCal = mealLogs.FirstOrDefault(m => m.MealType == MealType.Snack)?.Details.Sum(d => d.Calories) ?? 0;

        // 5. Nhật ký vận động hôm nay
        var caloriesBurned = await _context.ExerciseLogs
            .Where(e => e.UserId == userId && e.LoggedOn == today)
            .SumAsync(e => e.CaloriesBurned);

        // 6. Phương trình Cân bằng năng lượng:
        var remainingCalories = GoalPolicy.RemainingCalories(dailyTarget, caloriesConsumed, caloriesBurned);

        string statusClass;
        string statusBadgeBg;
        string statusText;
        if (remainingCalories > 50)
        {
            statusClass = "text-success";
            statusBadgeBg = "bg-success";
            statusText = "Còn ngân sách calo";
        }
        else if (remainingCalories >= -50 && remainingCalories <= 50)
        {
            statusClass = "text-warning-emphasis";
            statusBadgeBg = "bg-warning";
            statusText = "Đạt sát mục tiêu";
        }
        else
        {
            statusClass = "text-danger";
            statusBadgeBg = "bg-danger";
            statusText = "Đã vượt calo mục tiêu";
        }

        // 7. Khuyến nghị Macro
        var macroRec = _budgets.GetMacros(profile, budget);

        // 8. Tiến độ nước hôm nay
        var totalWaterToday = await _context.WaterLogs
            .Where(w => w.UserId == userId && w.LoggedOn == today)
            .SumAsync(w => w.AmountMl);

        var dailyWaterGoal = profile.DailyWaterGoalMl > 0 ? profile.DailyWaterGoalMl : 2000;
        var waterPercent = (int)Math.Clamp(Math.Round((double)totalWaterToday * 100 / dailyWaterGoal), 0, 100);

        // 9. Lịch sử cân nặng 30 ngày gần nhất (Chart.js)
        var thirtyDaysAgo = today.AddDays(-30);
        var weightLogs = await _context.WeightLogs
            .Where(w => w.UserId == userId && w.LoggedOn >= thirtyDaysAgo)
            .OrderBy(w => w.LoggedOn)
            .ThenBy(w => w.Id)
            .ToListAsync();

        var chartLabels = new List<string>();
        var chartWeights = new List<decimal>();
        var chartTargets = new List<decimal>();

        if (weightLogs.Any())
        {
            foreach (var log in weightLogs)
            {
                chartLabels.Add(log.LoggedOn.ToString("dd/MM"));
                chartWeights.Add(log.WeightKg);
                chartTargets.Add(targetWeight);
            }
        }
        else
        {
            chartLabels.Add((latestWeight?.LoggedOn ?? today).ToString("dd/MM"));
            chartWeights.Add(trackedWeight);
            chartTargets.Add(targetWeight);
        }

        // 10. Gợi ý 3-4 món ăn phù hợp ngân sách calo còn lại hôm nay
        var suggestedFoods = budget.Suspended ? new List<Food>() : await GetSuggestedFoodsAsync(profile, remainingCalories);

        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var reference = budget.ReferenceLabel.ToLower(vi);
        var suggestionNote = remainingCalories switch
        {
            > 50 => $"Còn {remainingCalories.ToString("N0", vi)} kcal so với {reference}.",
            >= -50 => $"Lượng calo hôm nay gần với {reference}.",
            _ => $"Đã vượt {reference} {Math.Abs(remainingCalories).ToString("N0", vi)} kcal."
        };

        var model = new DashboardViewModel
        {
            Budget = budget,
            FullName = user?.FullName ?? "Người dùng",
            AvatarUrl = profile.AvatarUrl,
            Gender = profile.Gender,
            Age = age,
            HeightCm = profile.HeightCm,
            CurrentWeightKg = trackedWeight,
            WeightMismatch = WeightMismatchViewModel.From(profile.CurrentWeightKg, latestWeight),
            Bmi = bmiInfo.Bmi,
            BmiCategory = bmiInfo.Category,
            BmiBadgeClass = bmiInfo.BadgeClass,
            Bmr = bmr,
            Tdee = tdee,
            DietType = profile.DietType,
            ActivityLevel = profile.ActivityLevel,
            Allergies = profile.Allergies,

            HasActiveGoal = hasGoal,
            GoalType = goalType,
            GoalTypeName = GetGoalTypeName(goalType),
            StartingWeightKg = startingWeight,
            TargetWeightKg = targetWeight,
            WeightDifferenceRemaining = Math.Abs(trackedWeight - targetWeight),
            GoalProgressPercent = progressPercent,
            TargetDate = activeGoal?.TargetDate,
            DaysRemaining = daysRemaining,

            DailyCalorieTarget = dailyTarget,
            CaloriesConsumedToday = caloriesConsumed,
            CaloriesBurnedToday = caloriesBurned,
            ExerciseCreditApplied = GoalPolicy.ExerciseCredit(caloriesBurned),
            RemainingCalories = remainingCalories,
            CalorieStatusClass = statusClass,
            CalorieStatusBadgeBg = statusBadgeBg,
            CalorieStatusText = statusText,

            ActualProtein = actualProtein,
            ActualCarb = actualCarb,
            ActualFat = actualFat,
            ActualFiber = actualFiber,
            ActualSugar = actualSugar,
            ActualSodium = actualSodium,

            RecommendedProteinGrams = macroRec?.ProteinGrams ?? 0,
            RecommendedCarbGrams = macroRec?.CarbGrams ?? 0,
            RecommendedFatGrams = macroRec?.FatGrams ?? 0,
            RecommendedProteinPercent = macroRec?.ProteinPercent ?? 0,
            RecommendedCarbPercent = macroRec?.CarbPercent ?? 0,
            RecommendedFatPercent = macroRec?.FatPercent ?? 0,
            MacroUnavailableReason = macroRec?.UnavailableReason,

            TotalWaterTodayMl = totalWaterToday,
            DailyWaterGoalMl = dailyWaterGoal,
            WaterProgressPercent = waterPercent,

            WeightChartLabels = chartLabels,
            WeightChartValues = chartWeights,
            TargetWeightChartValues = chartTargets,

            SuggestedFoods = suggestedFoods,
            SuggestionNote = suggestionNote,

            TotalMealItemsLogged = allDetails.Count,
            BreakfastCalories = breakfastCal,
            LunchCalories = lunchCal,
            DinnerCalories = dinnerCal,
            SnackCalories = snackCal
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddWater(int amountMl = 250)
    {
        var userId = GetCurrentUserId();
        var today = GoalPolicy.Today;
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return Unauthorized();

        if (amountMl < 50 || amountMl > 2000)
        {
            amountMl = 250;
        }

        var log = new WaterLog
        {
            UserId = userId,
            LoggedOn = today,
            AmountMl = amountMl,
            DailyGoalMl = profile.DailyWaterGoalMl,
            CreatedAt = DateTime.UtcNow
        };
        _context.WaterLogs.Add(log);
        await _context.SaveChangesAsync();

        var todayWater = await _context.WaterLogs
            .Where(w => w.UserId == userId && w.LoggedOn == today)
            .SumAsync(w => w.AmountMl);

        var dailyGoal = profile.DailyWaterGoalMl > 0 ? profile.DailyWaterGoalMl : 2000;
        var percent = (int)Math.Clamp(Math.Round((double)todayWater * 100 / dailyGoal), 0, 100);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new
            {
                success = true,
                added = amountMl,
                totalWater = todayWater,
                dailyGoal = dailyGoal,
                percent = percent
            });
        }

        TempData["SuccessMessage"] = $"Đã ghi nhận uống thêm +{amountMl} ml nước thành công!";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<Food>> GetSuggestedFoodsAsync(UserProfile profile, decimal remainingCalories)
    {
        // Giới hạn calo tìm kiếm
        decimal maxCalorieLimit;
        if (remainingCalories > 400)
        {
            maxCalorieLimit = Math.Min(remainingCalories, 650);
        }
        else if (remainingCalories > 150)
        {
            maxCalorieLimit = remainingCalories;
        }
        else
        {
            // Nếu calo còn rất ít hoặc âm, gợi ý món nhẹ < 120 kcal
            maxCalorieLimit = 120;
        }

        var query = _context.Foods
            .Include(f => f.Category)
            .Where(f => !f.IsHidden && f.Calories <= maxCalorieLimit && f.Calories >= 20);

        // Lọc theo chế độ ăn (DietType)
        if (profile.DietType == DietType.Vegetarian)
        {
            query = query.Where(f => f.IsVegetarian);
        }

        var candidates = await query.AsNoTracking().ToListAsync();
        var restrictions = new FoodRestrictions(FoodRestrictions.Split(profile.Allergies), FoodRestrictions.Split(profile.DislikedFoods));
        return candidates.Where(restrictions.Allows).Where(FoodText.CanServe)
            .OrderBy(x => Guid.NewGuid())
            .Take(4)
            .ToList();
    }

    private string GetGoalTypeName(GoalType type) => type switch
    {
        GoalType.Lose => "Giảm cân",
        GoalType.Gain => "Tăng cân",
        GoalType.Maintain => "Duy trì cân nặng",
        _ => "Duy trì cân nặng"
    };

    private int GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException("Người dùng chưa được xác thực hợp lệ.");
        }
        return userId;
    }
}
