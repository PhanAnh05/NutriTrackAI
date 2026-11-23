using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public interface IUserContextBuilder
{
    /// <summary>
    /// Thu thập toàn bộ hồ sơ nhân trắc, mục tiêu và nhật ký ăn/uống/tập hôm nay của người dùng
    /// </summary>
    Task<UserNutritionContext> BuildUserContextAsync(int userId, DateTime? date = null);

    /// <summary>
    /// Xuất chuỗi ngữ cảnh có cấu trúc dạng Markdown nạp vào Gemini System Prompt
    /// </summary>
    Task<string> BuildSystemPromptContextAsync(int userId, DateTime? date = null);
}

public class UserNutritionContext
{
    public string PlanSummary { get; set; } = "";
    public bool PlanSuspended { get; set; }
    public DietaryConstraints Constraints { get; set; } = DietaryConstraints.None;
    public MacroRecommendation? RecommendedMacros { get; set; }
    public bool UsesMaintenanceReference { get; set; }
    public string CalorieReferenceLabel => UsesMaintenanceReference ? CalorieBudget.MaintenanceLabel : CalorieBudget.PlanLabel;
    public decimal WeeklyRateKg { get; set; }
    public decimal Bmi { get; set; }
    public string BmiCategory { get; set; } = string.Empty;
    public decimal Bmr { get; set; }
    public decimal Tdee { get; set; }
    public DietType DietType { get; set; }
    internal List<string> LocalAllergyTerms { get; init; } = [];
    internal List<string> LocalDislikedFoods { get; init; } = [];
    public IReadOnlyList<string> StandardAllergens { get; init; } = [];
    public bool HasUnmappedAllergen { get; init; }

    // Mục tiêu
    public GoalType GoalType { get; set; }
    public string GoalTypeName { get; set; } = string.Empty;
    public decimal TargetWeightKg { get; set; }
    public int DailyCalorieTarget { get; set; }

    // Nhật ký trong ngày
    public DateTime SelectedDate { get; set; }
    public decimal CaloriesConsumedToday { get; set; }
    public decimal CaloriesBurnedToday { get; set; }
    public decimal ExerciseCreditApplied { get; set; }
    public decimal RemainingCalories { get; set; }
    public string CalorieStatusText { get; set; } = string.Empty;

    public decimal ActualProtein { get; set; }
    public decimal ActualCarb { get; set; }
    public decimal ActualFat { get; set; }
    public decimal ActualFiber { get; set; }
    public decimal ActualSugar { get; set; }
    public decimal ActualSodium { get; set; }

    public int TotalWaterTodayMl { get; set; }
    public int DailyWaterGoalMl { get; set; }
    public int WaterPercent { get; set; }

    public List<LoggedMealItemContext> LoggedMealItems { get; set; } = new();
    public List<string> LoggedExercises { get; set; } = new();
}

public class LoggedMealItemContext
{
    public MealType MealType { get; set; }
    public string MealTypeName { get; set; } = string.Empty;
    public string FoodName { get; set; } = string.Empty;
    public decimal Servings { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carb { get; set; }
    public decimal Fat { get; set; }
}
