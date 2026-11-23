using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 5: food_categories - Nhóm thực phẩm (Món chính, Rau củ, Trái cây, Đồ uống...)
/// </summary>
[Table("food_categories")]
public class FoodCategory
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<Food> Foods { get; set; } = new List<Food>();
}
