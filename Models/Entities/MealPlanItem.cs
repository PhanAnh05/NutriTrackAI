using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 10: meal_plan_items - Chi tiết các món trong thực đơn gợi ý / thực đơn mẫu
/// </summary>
[Table("meal_plan_items")]
public class MealPlanItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MealPlanId { get; set; }

    [Required]
    public MealType MealType { get; set; }

    [Required]
    public int FoodId { get; set; }

    [Range(0.1, 5000)]
    public decimal Servings { get; set; } = 1m;

    public int SortOrder { get; set; } = 0;

    public virtual MealPlan MealPlan { get; set; } = null!;
    public virtual Food Food { get; set; } = null!;
}
