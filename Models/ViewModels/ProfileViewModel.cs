using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class ProfileViewModel : IValidatableObject
{
    public bool HasActiveGoal { get; set; }
    public WeightMismatchViewModel? WeightMismatch { get; set; }
    [Display(Name = "Họ và tên")]
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên từ 2 đến 100 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    [Display(Name = "Ảnh đại diện mới")]
    public IFormFile? AvatarFile { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn giới tính.")]
    [Display(Name = "Giới tính")]
    public Gender Gender { get; set; } = Gender.Male;

    [Required(ErrorMessage = "Vui lòng nhập ngày sinh.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập chiều cao.")]
    [Range(100, 250, ErrorMessage = "Chiều cao phải từ 100 đến 250 cm.")]
    [Display(Name = "Chiều cao (cm)")]
    public decimal HeightCm { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập cân nặng hiện tại.")]
    [Range(20, 300, ErrorMessage = "Cân nặng phải từ 20 đến 300 kg.")]
    [Display(Name = "Cân nặng hiện tại (kg)")]
    [NutriTrackAI.Models.Validation.WeightPrecision]
    public decimal CurrentWeightKg { get; set; }

    public bool ConfirmLargeWeightChange { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập cân nặng mong muốn.")]
    [Range(20, 300, ErrorMessage = "Cân nặng mong muốn phải từ 20 đến 300 kg.")]
    [Display(Name = "Cân nặng mong muốn (kg)")]
    [NutriTrackAI.Models.Validation.WeightPrecision]
    public decimal TargetWeightKg { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn mức độ vận động.")]
    [Display(Name = "Mức độ vận động")]
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Sedentary;

    [Required(ErrorMessage = "Vui lòng chọn chế độ ăn.")]
    [Display(Name = "Chế độ ăn")]
    public DietType DietType { get; set; } = DietType.Normal;

    [StringLength(500)]
    [Display(Name = "Dị ứng thực phẩm (cách nhau dấu phẩy)")]
    public string? Allergies { get; set; }

    [StringLength(500)]
    [Display(Name = "Món không thích (cách nhau dấu phẩy)")]
    public string? DislikedFoods { get; set; }

    [StringLength(1000)]
    [Display(Name = "Ghi chú sức khỏe (tùy chọn)")]
    public string? HealthNotes { get; set; }
    public HealthScreeningForm Screening { get; set; } = new();
    public NutriTrackAI.Services.HealthAssessment? Health { get; set; }
    public string? PlanRestrictionMessage { get; set; }

    [Range(500, 10000, ErrorMessage = "Mục tiêu nước từ 500 đến 10.000 ml.")]
    [Display(Name = "Mục tiêu nước mỗi ngày (ml)")]
    public int DailyWaterGoalMl { get; set; } = 2000;

    // Chỉ số tính toán hiển thị trực tiếp
    public int Age { get; set; }
    public decimal Bmi { get; set; }
    public string BmiCategory { get; set; } = string.Empty;
    public string BmiBadgeClass { get; set; } = string.Empty;
    public decimal Bmr { get; set; }
    public decimal Tdee { get; set; }
    public int DailyCalorieTarget { get; set; }
    public string? StatusMessage { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = NutriTrackAI.Services.GoalPolicy.Today;
        var age = today.Year - DateOfBirth.Year;
        if (DateOfBirth.Date > today.AddYears(-age)) age--;

        if (!Enum.IsDefined(Gender) || !Enum.IsDefined(ActivityLevel) || !Enum.IsDefined(DietType))
            yield return new ValidationResult("Lựa chọn hồ sơ không hợp lệ.");
        if (age < 13 || age > 100)
        {
            yield return new ValidationResult("Độ tuổi phải từ 13 đến 100 tuổi.", new[] { nameof(DateOfBirth) });
        }
    }
}
