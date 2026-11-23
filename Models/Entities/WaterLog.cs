using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 13: water_logs - Nhật ký uống nước hằng ngày
/// </summary>
[Table("water_logs")]
public class WaterLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; }

    [Required]
    [Range(50, 2000, ErrorMessage = "Mỗi lần ghi nhận lượng nước phải từ 50 đến 2000 ml.")]
    public int AmountMl { get; set; }

    public int DailyGoalMl { get; set; } = 2000;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
}
