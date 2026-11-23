using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 9: meal_plans - Thực đơn gợi ý cá nhân hoặc thực đơn mẫu của Admin
/// </summary>
[Table("meal_plans")]
public class MealPlan
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Nullable: Nếu là thực đơn mẫu của Admin thì UserId = null
    /// </summary>
    public int? UserId { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public GoalType GoalType { get; set; }

    [Range(800, 6000)]
    public int TargetCalories { get; set; }

    public bool IsTemplate { get; set; } = false;

    public bool IsPublic { get; set; } = false;

    /// <summary>
    /// Giải thích lý do phù hợp (mức calo, tỷ lệ P/C/F, dị ứng đã loại trừ)
    /// </summary>
    [StringLength(1000)]
    public string? Explanation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ApplicationUser? User { get; set; }
    public virtual ICollection<MealPlanItem> Items { get; set; } = new List<MealPlanItem>();
}
