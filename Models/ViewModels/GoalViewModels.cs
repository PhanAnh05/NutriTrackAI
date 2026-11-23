using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class GoalIndexViewModel
{
    public NutriTrackAI.Services.CalorieBudget? Budget { get; set; }
    public NutriTrackAI.Services.PlanReview? Review { get; set; }
    public Goal? ActiveGoal { get; set; }
    public List<Goal> PastGoals { get; set; } = new();
    public decimal CurrentWeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public decimal Bmi { get; set; }
    public string BmiCategory { get; set; } = string.Empty;
    public string BmiBadgeClass { get; set; } = string.Empty;

    // Tiến trình mục tiêu
    public int DailyCalorieTarget { get; set; }
    public int ProgressPercent { get; set; }
    public decimal WeightChangeRemainingKg { get; set; }
    public int DaysRemaining { get; set; }
    public decimal TotalWeightChangePlanned { get; set; }
    public decimal TotalWeightChangeAchieved { get; set; }
}

public class GoalCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng chọn loại mục tiêu.")]
    [Display(Name = "Mục tiêu")]
    public GoalType GoalType { get; set; } = GoalType.Lose;

    [Display(Name = "Cân nặng bắt đầu (kg)")]
    public decimal StartWeight { get; set; }

    [Display(Name = "Chiều cao (cm)")]
    public decimal HeightCm { get; set; } = 170m;

    [Required(ErrorMessage = "Vui lòng nhập cân nặng mong muốn.")]
    [Range(20, 300, ErrorMessage = "Cân nặng mong muốn phải từ 20 đến 300 kg.")]
    [Display(Name = "Cân nặng mong muốn (kg)")]
    [NutriTrackAI.Models.Validation.WeightPrecision]
    public decimal? TargetWeight { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời gian thực hiện.")]
    [Range(1, 104, ErrorMessage = "Thời gian từ 1 đến 104 tuần.")]
    [Display(Name = "Thời gian thực hiện (tuần)")]
    public int DurationWeeks { get; set; } = 8;
    public bool UseManualWeeks { get; set; }

    // Kết quả tính toán & Đề xuất thời gian
    public int CalculatedDailyCalories { get; set; }
    public decimal WeeklyRateKg { get; set; }
    public bool WarningFlag { get; set; }
    public string? WarningMessage { get; set; }

    // Kiểm soát 3 mức độ (Xanh / Vàng / Đỏ)
    public GoalSafetyLevel SafetyLevel { get; set; } = GoalSafetyLevel.Safe;
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }
    public int RecommendedWeeks { get; set; } = 8;
    public int MinRecommendedWeeks { get; set; } = 4;
    public int MaxRecommendedWeeks { get; set; } = 12;

    // Giới hạn BMI & Ngưỡng cân nặng an toàn theo chiều cao
    public decimal CurrentBmi { get; set; }
    public decimal TargetBmi { get; set; }
    public decimal MinSafeWeight { get; set; }
    public decimal MaxNormalWeight { get; set; }
    public decimal MaxAllowedWeight { get; set; }
    public decimal SuggestedSafeWeight { get; set; }

    public bool Introductory { get; set; }
    public bool AcknowledgeIntensePace { get; set; }
    public NutriTrackAI.Services.CalorieTargetResult? Analysis { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(GoalType))
            yield return new ValidationResult("Loại mục tiêu không hợp lệ.", new[] { nameof(GoalType) });
    }
}
