using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 7: meal_logs - Nhật ký bữa ăn theo ngày và buổi (Sáng/Trưa/Tối/Phụ)
/// </summary>
[Table("meal_logs")]
public class MealLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; }

    [Required]
    public MealType MealType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<MealLogDetail> Details { get; set; } = new List<MealLogDetail>();
}
