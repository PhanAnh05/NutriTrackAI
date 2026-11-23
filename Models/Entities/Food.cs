using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 6: foods - Danh mục món ăn và thành phần dinh dưỡng
/// </summary>
[Table("foods")]
public class Food
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Đơn vị hiển thị khẩu phần chuẩn (ví dụ: "100g", "1 tô (450g)", "1 quả")
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ServingName { get; set; } = "100g";

    /// <summary>
    /// Khối lượng/định lượng khẩu phần chuẩn (ví dụ: 100 cho 100g, hoặc 1 cho 1 phần)
    /// </summary>
    [Range(0.1, 5000)]
    public decimal ServingSize { get; set; } = 100m;

    [Range(0, 5000)]
    public decimal Calories { get; set; }

    [Range(0, 500)]
    public decimal Protein { get; set; }

    [Range(0, 500)]
    public decimal Carb { get; set; }

    [Range(0, 500)]
    public decimal Fat { get; set; }

    [Range(0, 200)]
    public decimal Fiber { get; set; }

    [Range(0, 500)]
    public decimal Sugar { get; set; }

    [Range(0, 20000)]
    public decimal Sodium { get; set; } // mg

    /// <summary>
    /// Thành phần chính phục vụ tìm kiếm và kiểm tra chế độ ăn (ví dụ: "gạo, thịt bò, hành")
    /// </summary>
    [StringLength(500)]
    public string? Ingredients { get; set; }

    /// <summary>
    /// Nhãn dị ứng chuẩn hóa (ví dụ: "hải sản, tôm, sữa, đậu phộng, trứng, gluten")
    /// </summary>
    [StringLength(300)]
    public string? AllergenNotes { get; set; }

    [NotMapped]
    public bool HasAllergenNotes => !string.IsNullOrWhiteSpace(AllergenNotes)
        && !string.Equals(AllergenNotes.Trim(), "Không", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cảnh báo sức khỏe (ví dụ: "Nhiều đường và chất béo bão hòa", "Hàm lượng natri cao")
    /// </summary>
    [StringLength(500)]
    public string? HealthWarning { get; set; }

    /// <summary>
    /// Đánh dấu món chay để lọc nhanh cho chế độ ăn Vegetarian
    /// </summary>
    public bool IsVegetarian { get; set; } = false;

    public bool IsHidden { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual FoodCategory Category { get; set; } = null!;
    public virtual ICollection<MealLogDetail> MealLogDetails { get; set; } = new List<MealLogDetail>();
    public virtual ICollection<MealPlanItem> MealPlanItems { get; set; } = new List<MealPlanItem>();
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
