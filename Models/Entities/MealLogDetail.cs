using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 8: meal_log_details - Chi tiết món ăn trong từng bữa (lưu snapshot dinh dưỡng thực tế)
/// </summary>
[Table("meal_log_details")]
public class MealLogDetail
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MealLogId { get; set; }

    [Required]
    public int FoodId { get; set; }

    /// <summary>
    /// Định lượng người dùng nhập (cùng đơn vị với ServingSize hoặc số khẩu phần)
    /// </summary>
    [Range(0.01, 5000, ErrorMessage = "Khẩu phần phải lớn hơn 0 và không vượt quá 5000.")]
    public decimal Servings { get; set; }

    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carb { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public decimal Sodium { get; set; }

    public virtual MealLog MealLog { get; set; } = null!;
    public virtual Food Food { get; set; } = null!;
}
