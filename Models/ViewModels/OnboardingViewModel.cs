using NutriTrackAI.Services;
using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class OnboardingViewModel : IValidatableObject
{
    // ==========================================
    // BƯỚC 1: Thông tin nhân trắc học
    // ==========================================
    [Required(ErrorMessage = "Vui lòng chọn giới tính.")]
    [Display(Name = "Giới tính")]
    public Gender Gender { get; set; } = Gender.Male;

    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateTime DateOfBirth { get; set; } = GoalPolicy.Today.AddYears(-22);

    [Required(ErrorMessage = "Vui lòng nhập chiều cao.")]
    [Range(100, 250, ErrorMessage = "Chiều cao phải từ 100 đến 250 cm.")]
    [Display(Name = "Chiều cao (cm)")]
    public decimal HeightCm { get; set; } = 170m;

    [Required(ErrorMessage = "Vui lòng nhập cân nặng hiện tại.")]
    [Range(20, 300, ErrorMessage = "Cân nặng phải từ 20 đến 300 kg.")]
    [Display(Name = "Cân nặng hiện tại (kg)")]
    [NutriTrackAI.Models.Validation.WeightPrecision]
    public decimal CurrentWeightKg { get; set; } = 70m;

    // ==========================================
    // BƯỚC 2: Mục tiêu cân nặng
    // ==========================================
    [Required(ErrorMessage = "Vui lòng chọn mục tiêu cân nặng.")]
    [Display(Name = "Mục tiêu")]
    public GoalType GoalType { get; set; } = GoalType.Lose;

    [Required(ErrorMessage = "Vui lòng nhập cân nặng mong muốn.")]
    [Range(20, 300, ErrorMessage = "Cân nặng mong muốn phải từ 20 đến 300 kg.")]
    [Display(Name = "Cân nặng mong muốn (kg)")]
    [NutriTrackAI.Models.Validation.WeightPrecision]
    public decimal? TargetWeightKg { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời gian dự kiến.")]
    [Range(1, 104, ErrorMessage = "Thời gian thực hiện từ 1 đến 104 tuần.")]
    [Display(Name = "Thời gian dự kiến (tuần)")]
    public int DurationWeeks { get; set; } = 10;
    public bool UseManualWeeks { get; set; }
    public bool AcknowledgeIntensePace { get; set; }
    public CalorieTargetResult? Analysis { get; set; }

    // ==========================================
    // BƯỚC 3: Lối sống & Thói quen dinh dưỡng
    // ==========================================
    [Required(ErrorMessage = "Vui lòng chọn mức độ vận động.")]
    [Display(Name = "Mức độ vận động")]
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Moderate;

    [Required(ErrorMessage = "Vui lòng chọn chế độ ăn.")]
    [Display(Name = "Chế độ ăn")]
    public DietType DietType { get; set; } = DietType.Normal;

    [StringLength(500)]
    [Display(Name = "Dị ứng thực phẩm (cách nhau bởi dấu phẩy)")]
    public string? Allergies { get; set; }

    [StringLength(500)]
    [Display(Name = "Món không thích (cách nhau bởi dấu phẩy)")]
    public string? DislikedFoods { get; set; }

    [StringLength(1000)]
    [Display(Name = "Ghi chú sức khỏe (tùy chọn)")]
    public string? HealthNotes { get; set; }
    public HealthScreeningForm Screening { get; set; } = new();
    public bool Introductory { get; set; }

    [Range(500, 10000)]
    [Display(Name = "Mục tiêu nước mỗi ngày (ml)")]
    public int DailyWaterGoalMl { get; set; } = 2000;

    // ==========================================
    // BƯỚC 4: Kết quả tính toán Preview
    // ==========================================
    public int Age { get; set; }
    public decimal Bmi { get; set; }
    public string BmiCategory { get; set; } = string.Empty;
    public string BmiBadgeClass { get; set; } = string.Empty;
    public decimal Bmr { get; set; }
    public decimal Tdee { get; set; }
    public int DailyCalorieTarget { get; set; }
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

    // Macro phân bổ
    public decimal ProteinGrams { get; set; }
    public decimal CarbGrams { get; set; }
    public decimal FatGrams { get; set; }
    public int ProteinPercent { get; set; }
    public int CarbPercent { get; set; }
    public int FatPercent { get; set; }
    public string? MacroUnavailableReason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = NutriTrackAI.Services.GoalPolicy.Today;
        var age = today.Year - DateOfBirth.Year;
        if (DateOfBirth.Date > today.AddYears(-age)) age--;

        if (age < 13 || age > 100)
        {
            yield return new ValidationResult("Độ tuổi phải từ 13 đến 100 tuổi.", new[] { nameof(DateOfBirth) });
        }

        if (!Enum.IsDefined(GoalType) || !Enum.IsDefined(Gender) || !Enum.IsDefined(ActivityLevel))
            yield return new ValidationResult("Lựa chọn hồ sơ không hợp lệ.");
    }
}
