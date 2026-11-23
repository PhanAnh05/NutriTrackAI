using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 12: exercise_logs - Nhật ký vận động hằng ngày của người dùng
/// </summary>
[Table("exercise_logs")]
public class ExerciseLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; }

    public int? ExerciseId { get; set; }

    [StringLength(120)]
    public string? CustomName { get; set; }

    [Required]
    [Range(1, 300, ErrorMessage = "Thời gian vận động phải từ 1 đến 300 phút.")]
    public int DurationMin { get; set; }

    [Required]
    public ExerciseIntensity Intensity { get; set; } = ExerciseIntensity.Moderate;

    [Range(0, 5000)]
    public decimal CaloriesBurned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Exercise? Exercise { get; set; }
}
