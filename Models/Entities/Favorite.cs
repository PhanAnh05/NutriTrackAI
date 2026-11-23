using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 14: favorites - Danh sách món ăn yêu thích của người dùng
/// </summary>
[Table("favorites")]
public class Favorite
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int FoodId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Food Food { get; set; } = null!;
}
