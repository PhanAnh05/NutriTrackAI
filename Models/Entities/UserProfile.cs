using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 2: user_profiles - Hồ sơ sức khỏe & nhân trắc học (Quan hệ 1-1 với users)
/// </summary>
[Table("user_profiles")]
public class UserProfile
{
    [Key, ForeignKey(nameof(User))]
    public int UserId { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    [Required]
    public Gender Gender { get; set; } = Gender.Male;

    [Required]
    [DataType(DataType.Date)]
    public DateTime DateOfBirth { get; set; }

    [Range(100, 250, ErrorMessage = "Chiều cao phải nằm trong khoảng 100 đến 250 cm.")]
    public decimal HeightCm { get; set; }

    [Range(20, 300, ErrorMessage = "Cân nặng phải nằm trong khoảng 20 đến 300 kg.")]
    public decimal CurrentWeightKg { get; set; }

    [Range(20, 300, ErrorMessage = "Cân nặng mong muốn phải nằm trong khoảng 20 đến 300 kg.")]
    public decimal TargetWeightKg { get; set; }

    [Required]
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Sedentary;

    [Required]
    public DietType DietType { get; set; } = DietType.Normal;

    /// <summary>
    /// Danh sách dị ứng cách nhau bởi dấu phẩy (ví dụ: "hải sản, đậu phộng, tôm")
    /// </summary>
    [StringLength(500)]
    public string? Allergies { get; set; }

    /// <summary>
    /// Món không thích cách nhau bởi dấu phẩy (ví dụ: "hành tây, khổ qua")
    /// </summary>
    [StringLength(500)]
    public string? DislikedFoods { get; set; }

    /// <summary>
    /// Ghi chú sức khỏe tùy chọn (không dùng để chẩn đoán bệnh)
    /// </summary>
    [StringLength(1000)]
    public string? HealthNotes { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? HealthScreeningJson { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public MedicalConditions MedicalConditions { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool DoctorAwareOfPlan { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public DateTime? HealthScreeningAt { get; set; }

    [Range(500, 10000)]
    public int DailyWaterGoalMl { get; set; } = 2000;

    public bool OnboardingCompleted { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
}
