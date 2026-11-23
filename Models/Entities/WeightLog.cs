using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 3: weight_logs - Nhật ký theo dõi cân nặng theo ngày
/// </summary>
[Table("weight_logs")]
public class WeightLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; }

    [Required]
    [Range(20, 300, ErrorMessage = "Cân nặng phải từ 20 đến 300 kg.")]
    public decimal WeightKg { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    [StringLength(500)]
    public string? PhotoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
}
