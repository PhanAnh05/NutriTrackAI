using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Areas.Admin.Models;

public class AdminDashboardViewModel
{
    // Thống kê tổng hợp người dùng
    public int TotalUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersToday { get; set; }

    // Thống kê Chatbot AI
    public int TotalChatSessions { get; set; }
    public int TotalChatMessages { get; set; }

    // Thống kê dữ liệu thực phẩm & dinh dưỡng
    public int TotalFoods { get; set; }
    public int TotalCategories { get; set; }
    public int TotalMealLogsLogged { get; set; }

    // Thống kê mục tiêu người dùng
    public int GoalTypeLoseCount { get; set; }
    public int GoalTypeGainCount { get; set; }
    public int GoalTypeMaintainCount { get; set; }

    // Top 10 món ăn được ghi nhận nhiều nhất
    public List<TopFoodDto> TopFoods { get; set; } = new();

    // 5 người dùng mới nhất
    public List<AdminUserItemDto> RecentUsers { get; set; } = new();
}

public class TopFoodDto
{
    public int FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int LogCount { get; set; }
    public decimal TotalCaloriesLogged { get; set; }
}

public class AdminUserListViewModel
{
    public List<AdminUserItemDto> Users { get; set; } = new();
    public string? SearchTerm { get; set; }
    public string? RoleFilter { get; set; }
    public string? StatusFilter { get; set; } // "all", "active", "locked"
}

public class AdminUserItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = "User";
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminFoodEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên món ăn.")]
    [StringLength(150, ErrorMessage = "Tên món tối đa 150 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn nhóm thực phẩm.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đơn vị khẩu phần (ví dụ: 1 tô, 1 đĩa, 1 chén, 100g).")]
    [StringLength(50)]
    public string ServingName { get; set; } = "1 phần (100g)";

    [Range(0.1, 5000, ErrorMessage = "Trọng lượng chuẩn phải từ 0.1 đến 5.000.")]
    public decimal ServingSize { get; set; } = 100m;

    [Range(0, 5000, ErrorMessage = "Calo phải từ 0 đến 5.000 kcal.")]
    public decimal Calories { get; set; }

    [Range(0, 500, ErrorMessage = "Protein phải từ 0 đến 500g.")]
    public decimal Protein { get; set; }

    [Range(0, 500, ErrorMessage = "Carb phải từ 0 đến 500g.")]
    public decimal Carb { get; set; }

    [Range(0, 500, ErrorMessage = "Fat phải từ 0 đến 500g.")]
    public decimal Fat { get; set; }

    [Range(0, 200, ErrorMessage = "Chất xơ phải từ 0 đến 200g.")]
    public decimal Fiber { get; set; }

    [Range(0, 500, ErrorMessage = "Đường phải từ 0 đến 500g.")]
    public decimal Sugar { get; set; }

    [Range(0, 10000, ErrorMessage = "Natri phải từ 0 đến 10.000mg.")]
    public decimal Sodium { get; set; }

    public string? Ingredients { get; set; }

    [StringLength(250)]
    public string? AllergenNotes { get; set; }

    [StringLength(250)]
    public string? HealthWarning { get; set; }

    public bool IsVegetarian { get; set; } = false;
    public bool IsHidden { get; set; } = false;

    public string? ImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }

    public List<FoodCategory> AvailableCategories { get; set; } = new();
}

public class AdminCategoryEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên nhóm thực phẩm.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã slug (ví dụ: mon-chinh, tinh-bot).")]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class AdminArticleEditViewModel
{
    public int Id { get; set; }

    [Required]
    public ArticleType Type { get; set; } = ArticleType.Article;

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề bài viết / FAQ.")]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung chi tiết.")]
    public string Body { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;
}

public class AdminMealPlanEditViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên thực đơn mẫu.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public GoalType GoalType { get; set; } = GoalType.Lose;

    [Range(800, 6000, ErrorMessage = "Mức calo mục tiêu từ 800 đến 6.000 kcal.")]
    public int TargetCalories { get; set; } = 1500;

    public bool IsPublic { get; set; } = true;

    [StringLength(1000)]
    public string? Explanation { get; set; }

    public List<MealPlanItemDto> Items { get; set; } = new();
}
