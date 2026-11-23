using System.Text;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public class UserContextBuilder : IUserContextBuilder
{
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _nutritionCalculator;
    private readonly ILogger<UserContextBuilder> _logger;
    private readonly CalorieBudgetService _budgets;
    private readonly AdaptiveCalorieService _adaptive;

    public UserContextBuilder(
        AppDbContext context,
        INutritionCalculatorService nutritionCalculator,
        ILogger<UserContextBuilder> logger, CalorieBudgetService budgets, AdaptiveCalorieService adaptive)
    {
        _context = context;
        _nutritionCalculator = nutritionCalculator;
        _logger = logger;
        _budgets = budgets; _adaptive = adaptive;
    }

    public async Task<UserNutritionContext> BuildUserContextAsync(int userId, DateTime? date = null)
    {
        var targetDate = (date ?? GoalPolicy.Today).Date;

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            return new UserNutritionContext
            {
                PlanSuspended = true,
                UsesMaintenanceReference = true,
                SelectedDate = targetDate
            };
        }

        var age = _nutritionCalculator.CalculateAge(profile.DateOfBirth);
        var bmiInfo = _nutritionCalculator.GetBmiInfo(profile.CurrentWeightKg, profile.HeightCm, age);
        var bmr = _nutritionCalculator.CalculateBmr(profile.Gender, profile.CurrentWeightKg, profile.HeightCm, age);
        var budget = await _budgets.GetAsync(userId);
        var activeGoal = budget.Goal;
        var goalType = activeGoal?.GoalType ?? GoalType.Maintain;
        var dailyTarget = budget.TargetCalories;
        var review = await _adaptive.ReviewAsync(userId);

        // Bữa ăn hôm nay
        var mealLogs = await _context.MealLogs
            .Include(m => m.Details)
                .ThenInclude(d => d.Food)
            .Where(m => m.UserId == userId && m.LoggedOn == targetDate)
            .ToListAsync();

        var allDetails = mealLogs.SelectMany(m => m.Details).ToList();
        var caloriesConsumed = allDetails.Sum(d => d.Calories);
        var totalP = allDetails.Sum(d => d.Protein);
        var totalC = allDetails.Sum(d => d.Carb);
        var totalF = allDetails.Sum(d => d.Fat);
        var totalFiber = allDetails.Sum(d => d.Fiber);
        var totalSugar = allDetails.Sum(d => d.Sugar);
        var totalSodium = allDetails.Sum(d => d.Sodium);

        // Vận động hôm nay
        var exerciseLogs = await _context.ExerciseLogs
            .Include(e => e.Exercise)
            .Where(e => e.UserId == userId && e.LoggedOn == targetDate)
            .ToListAsync();

        var caloriesBurned = exerciseLogs.Sum(e => e.CaloriesBurned);

        // Nước uống hôm nay
        var waterLogs = await _context.WaterLogs
            .Where(w => w.UserId == userId && w.LoggedOn == targetDate)
            .ToListAsync();

        var totalWater = waterLogs.Sum(w => w.AmountMl);
        var dailyWaterGoal = profile.DailyWaterGoalMl > 0 ? profile.DailyWaterGoalMl : 2000;
        var waterPercent = (int)Math.Clamp(Math.Round((double)totalWater * 100 / dailyWaterGoal), 0, 100);

        // Công thức Cân bằng Năng lượng (Mục 4.5 GEMINI.md)
        var remainingCalories = GoalPolicy.RemainingCalories(dailyTarget, caloriesConsumed, caloriesBurned);

        string statusText;
        if (budget.UsesMaintenanceReference)
        {
            statusText = "So với mốc duy trì";
        }
        else if (remainingCalories > 50)
        {
            statusText = "Còn ngân sách calo";
        }
        else if (remainingCalories >= -50 && remainingCalories <= 50)
        {
            statusText = "Đạt sát mục tiêu";
        }
        else
        {
            statusText = "Đã vượt calo mục tiêu";
        }

        var loggedItems = allDetails.Select(d => new LoggedMealItemContext
        {
            MealType = d.MealLog.MealType,
            MealTypeName = GetMealTypeName(d.MealLog.MealType),
            FoodName = d.Food.Name,
            Servings = d.Servings,
            ServingUnit = d.Food.ServingName,
            Calories = d.Calories,
            Protein = d.Protein,
            Carb = d.Carb,
            Fat = d.Fat
        }).ToList();

        var loggedExercises = exerciseLogs.Select(e =>
            $"{e.Exercise?.Name ?? "Vận động"} ({e.DurationMin} phút, tiêu hao {e.CaloriesBurned:N0} kcal)"
        ).ToList();
        var allergyTerms = FoodRestrictions.Split(profile.Allergies);
        var (allergenGroups, hasUnmappedAllergen) = AllergenGroups.Normalize(allergyTerms);

        return new UserNutritionContext
        {
            PlanSuspended = budget.Suspended,
            Constraints = budget.Health.Constraints,
            RecommendedMacros = _budgets.GetMacros(profile, budget),
            UsesMaintenanceReference = budget.UsesMaintenanceReference,
            PlanSummary = budget.Suspended
                ? "Ứng dụng chỉ hỗ trợ theo dõi nhật ký, chưa áp dụng kế hoạch calo tự động."
                : activeGoal == null ? budget.Explanation
                : $"{budget.Phase}. {budget.Explanation} Ngày mong muốn: {activeGoal.TargetDate:dd/MM/yyyy}; ngày dự kiến theo mức hiện tại: {budget.EstimatedDate:dd/MM/yyyy}; đánh giá tiếp: {budget.NextReviewOn:dd/MM/yyyy}. Mức điều chỉnh: {budget.Adjustment:0.##} kcal/ngày. Đánh giá: {review.Message} Mức đề xuất chưa áp dụng: {review.ProposedAdjustment?.ToString("0.##") ?? "không có"} kcal/ngày.",
            WeeklyRateKg = GoalPolicy.WeeklyRate(budget.Adjustment),
            Bmi = bmiInfo.Bmi,
            BmiCategory = bmiInfo.Category,
            Bmr = bmr,
            Tdee = budget.Tdee,
            DietType = profile.DietType,
            LocalAllergyTerms = allergyTerms.ToList(),
            LocalDislikedFoods = FoodRestrictions.Split(profile.DislikedFoods).ToList(),
            StandardAllergens = allergenGroups,
            HasUnmappedAllergen = hasUnmappedAllergen,

            GoalType = goalType,
            GoalTypeName = GetGoalTypeName(goalType),
            TargetWeightKg = activeGoal?.TargetWeight ?? profile.TargetWeightKg,
            DailyCalorieTarget = dailyTarget,

            SelectedDate = targetDate,
            CaloriesConsumedToday = caloriesConsumed,
            CaloriesBurnedToday = caloriesBurned,
            ExerciseCreditApplied = GoalPolicy.ExerciseCredit(caloriesBurned),
            RemainingCalories = remainingCalories,
            CalorieStatusText = statusText,

            ActualProtein = totalP,
            ActualCarb = totalC,
            ActualFat = totalF,
            ActualFiber = totalFiber,
            ActualSugar = totalSugar,
            ActualSodium = totalSodium,

            TotalWaterTodayMl = totalWater,
            DailyWaterGoalMl = dailyWaterGoal,
            WaterPercent = waterPercent,

            LoggedMealItems = loggedItems,
            LoggedExercises = loggedExercises
        };
    }

    public async Task<string> BuildSystemPromptContextAsync(int userId, DateTime? date = null) =>
        FormatSystemData(await BuildUserContextAsync(userId, date));

    internal static string FormatSystemData(UserNutritionContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== [TRẠNG THÁI KẾ HOẠCH CALO] ===");
        sb.AppendLine(ctx.PlanSummary);
        if (ctx.PlanSuspended)
        {
            sb.AppendLine($"{CalorieBudget.MaintenanceLabel} chỉ dùng làm mốc tham khảo.");
            sb.AppendLine($"Calo đã ghi hôm nay: {ctx.CaloriesConsumedToday:N0} kcal. Nước đã ghi: {ctx.TotalWaterTodayMl:N0} ml.");
            return sb.ToString();
        }
        sb.AppendLine($"- BMI: {ctx.Bmi:0.0} ({ctx.BmiCategory}) | BMR: {ctx.Bmr:N0} kcal/ngày");
        sb.AppendLine($"- {CalorieBudget.MaintenanceLabel}: {ctx.Tdee:N0} kcal/ngày");
        sb.AppendLine($"- Chế độ ăn uống: {ctx.DietType.GetVietnameseLabel()}");
        if (!ctx.UsesMaintenanceReference)
        {
            sb.AppendLine($"- Loại mục tiêu: {ctx.GoalTypeName} (Cân nặng đích: {ctx.TargetWeightKg:0.0} kg)");
            sb.AppendLine($"- Tốc độ ước tính theo calo áp dụng: {ctx.WeeklyRateKg:0.##} kg/tuần");
            sb.AppendLine($"- {ctx.CalorieReferenceLabel}: {ctx.DailyCalorieTarget:N0} kcal/ngày");
        }
        if (ctx.RecommendedMacros is { UnavailableReason: null } macros)
            sb.AppendLine($"- Macro tham khảo/ngày: Chất đạm {macros.ProteinGrams:0.0} g | Tinh bột {macros.CarbGrams:0.0} g | Chất béo {macros.FatGrams:0.0} g");
        else if (ctx.RecommendedMacros?.UnavailableReason is { } reason)
            sb.AppendLine($"- Macro tham khảo: {reason}");
        sb.AppendLine();

        sb.AppendLine($"=== [NHẬT KÝ THỰC TẾ HÔM NAY - {ctx.SelectedDate:dd/MM/yyyy}] ===");
        sb.AppendLine($"- Calo đã nạp từ bữa ăn: {ctx.CaloriesConsumedToday:N0} kcal");
        sb.AppendLine($"- Vận động đã ghi: {ctx.CaloriesBurnedToday:N0} kcal; phần cộng lại: {ctx.ExerciseCreditApplied:N0} kcal.");
        sb.AppendLine($"- {(ctx.UsesMaintenanceReference ? "Chênh lệch so với mốc duy trì" : "Calo còn lại")}: {ctx.RemainingCalories:N0} kcal (Trạng thái: {ctx.CalorieStatusText})");
        sb.AppendLine($"- Dinh dưỡng đa lượng đã nạp: Chất đạm {ctx.ActualProtein:0.0} g | Tinh bột {ctx.ActualCarb:0.0} g | Chất béo {ctx.ActualFat:0.0} g");
        sb.AppendLine($"- Dinh dưỡng vi lượng: Chất xơ {ctx.ActualFiber:0.0}g | Đường {ctx.ActualSugar:0.0}g | Natri {ctx.ActualSodium:N0}mg");
        sb.AppendLine(ctx.Constraints.WaterPerDoctor
            ? $"- Nước đã ghi: {ctx.TotalWaterTodayMl:N0} ml. {GoalPolicy.WaterPerDoctorNote}"
            : $"- Nước uống hôm nay: {ctx.TotalWaterTodayMl:N0} / {ctx.DailyWaterGoalMl:N0} ml ({ctx.WaterPercent}%)");

        if (ctx.LoggedExercises.Any())
        {
            sb.AppendLine($"- Hoạt động vận động hôm nay: {string.Join("; ", ctx.LoggedExercises)}");
        }
        else
        {
            sb.AppendLine("- Hoạt động vận động hôm nay: Chưa ghi nhận buổi tập nào.");
        }

        sb.AppendLine("- Danh sách các món đã ăn hôm nay:");
        if (ctx.LoggedMealItems.Any())
        {
            var grouped = ctx.LoggedMealItems.GroupBy(i => i.MealTypeName);
            foreach (var group in grouped)
            {
                var itemsStr = string.Join(", ", group.Select(i => $"{i.FoodName} ({i.Servings:0.##} {i.ServingUnit}, {i.Calories:N0} kcal)"));
                sb.AppendLine($"  + {group.Key}: {itemsStr}");
            }
        }
        else
        {
            sb.AppendLine("  (Chưa có món ăn nào được ghi nhận trong ngày hôm nay)");
        }

        return sb.ToString();
    }

    private string GetGoalTypeName(GoalType goalType) => goalType switch
    {
        GoalType.Lose => "Giảm cân",
        GoalType.Gain => "Tăng cân",
        GoalType.Maintain => "Duy trì cân nặng",
        _ => "Duy trì cân nặng"
    };

    private string GetMealTypeName(MealType mealType) => mealType switch
    {
        MealType.Breakfast => "Bữa sáng",
        MealType.Lunch => "Bữa trưa",
        MealType.Dinner => "Bữa tối",
        MealType.Snack => "Bữa phụ",
        _ => "Bữa ăn"
    };
}
