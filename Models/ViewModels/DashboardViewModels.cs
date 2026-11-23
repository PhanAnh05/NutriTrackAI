using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class DashboardViewModel
{
    public NutriTrackAI.Services.CalorieBudget Budget { get; set; } = null!;
    // 1. Thông tin nhân trắc & Người dùng
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public Gender Gender { get; set; }
    public int Age { get; set; }
    public decimal HeightCm { get; set; }
    public decimal CurrentWeightKg { get; set; }
    public WeightMismatchViewModel? WeightMismatch { get; set; }
    public decimal Bmi { get; set; }
    public string BmiCategory { get; set; } = "Bình thường";
    public string BmiBadgeClass { get; set; } = "badge-custom-emerald";
    public decimal Bmr { get; set; }
    public decimal Tdee { get; set; }
    public DietType DietType { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public string? Allergies { get; set; }

    // 2. Tiến độ mục tiêu (Goal)
    public bool HasActiveGoal { get; set; }
    public GoalType GoalType { get; set; }
    public string GoalTypeName { get; set; } = "Duy trì cân nặng";
    public decimal StartingWeightKg { get; set; }
    public decimal TargetWeightKg { get; set; }
    public decimal WeightDifferenceRemaining { get; set; }
    public decimal GoalProgressPercent { get; set; }
    public DateTime? TargetDate { get; set; }
    public int DaysRemaining { get; set; }

    // 3. Phương trình Cân bằng năng lượng hôm nay
    // Calo còn lại = Calo mục tiêu - Calo đã ăn + Calo vận động
    public int DailyCalorieTarget { get; set; } = 2000;
    public decimal CaloriesConsumedToday { get; set; }
    public decimal CaloriesBurnedToday { get; set; }
    public decimal ExerciseCreditApplied { get; set; }
    public decimal RemainingCalories { get; set; }
    public string CalorieStatusClass { get; set; } = "text-success"; // text-success, text-warning, text-danger
    public string CalorieStatusBadgeBg { get; set; } = "bg-success";
    public string CalorieStatusText { get; set; } = "Còn ngân sách calo";

    // 4. Phân bổ Macro dinh dưỡng hôm nay (Thực tế vs Khuyến nghị)
    public decimal ActualProtein { get; set; }
    public decimal ActualCarb { get; set; }
    public decimal ActualFat { get; set; }
    public decimal ActualFiber { get; set; }
    public decimal ActualSugar { get; set; }
    public decimal ActualSodium { get; set; }
    public string? MacroUnavailableReason { get; set; }

    public decimal RecommendedProteinGrams { get; set; }
    public decimal RecommendedCarbGrams { get; set; }
    public decimal RecommendedFatGrams { get; set; }
    public int RecommendedProteinPercent { get; set; }
    public int RecommendedCarbPercent { get; set; }
    public int RecommendedFatPercent { get; set; }

    // 5. Tiến độ uống nước trong ngày
    public int TotalWaterTodayMl { get; set; }
    public int DailyWaterGoalMl { get; set; } = 2000;
    public int WaterProgressPercent { get; set; }

    // 6. Biểu đồ cân nặng 30 ngày gần nhất
    public List<string> WeightChartLabels { get; set; } = new();
    public List<decimal> WeightChartValues { get; set; } = new();
    public List<decimal> TargetWeightChartValues { get; set; } = new();

    // 7. Gợi ý món ăn phù hợp với calo còn lại
    public List<Food> SuggestedFoods { get; set; } = new();
    public string SuggestionNote { get; set; } = string.Empty;

    // 8. Tóm tắt nhanh bữa ăn hôm nay
    public int TotalMealItemsLogged { get; set; }
    public decimal BreakfastCalories { get; set; }
    public decimal LunchCalories { get; set; }
    public decimal DinnerCalories { get; set; }
    public decimal SnackCalories { get; set; }
}
