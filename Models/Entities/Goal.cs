using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 4: goals - Mục tiêu cân nặng cá nhân (chỉ 1 mục tiêu IsActive = true tại 1 thời điểm)
/// </summary>
[Table("goals")]
public class Goal
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public GoalType GoalType { get; set; }

    [Range(20, 300)]
    public decimal StartWeight { get; set; }

    [Range(20, 300)]
    public decimal TargetWeight { get; set; }

    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    [DataType(DataType.Date)]
    [ConcurrencyCheck]
    public DateTime TargetDate { get; set; }

    /// <summary>
    /// Tốc độ thay đổi dự kiến mỗi tuần (kg/tuần)
    /// </summary>
    public decimal WeeklyRateKg { get; set; }

    /// <summary>
    /// Mức calo khuyến nghị nạp mỗi ngày (kcal)
    /// </summary>
    [Range(800, 6000)]
    public int DailyCalorieTarget { get; set; }

    // Nullable for goals created before adaptive planning. Concurrency protects stale confirmations.
    [ConcurrencyCheck]
    public string? CaloriePlanJson { get; set; }

    [ConcurrencyCheck]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Cờ cảnh báo khi người dùng đặt tốc độ giảm/tăng cân vượt ngưỡng an toàn
    /// </summary>
    public bool WarningFlag { get; set; } = false;

    [StringLength(500)]
    public string? WarningMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
}
