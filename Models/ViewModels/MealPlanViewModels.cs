using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;

namespace NutriTrackAI.Models.ViewModels;

public class MealPlanIndexViewModel
{
    public DietaryConstraints DietaryConstraints { get; set; } = DietaryConstraints.None;
    public MacroRecommendation? RecommendedMacros { get; set; }
    // Thực đơn hiện tại đang xem/vừa tạo
    public MealPlanResult? CurrentPlan { get; set; }

    // Danh sách thực đơn mẫu công khai của Admin
    public List<MealPlanTemplateSummaryDto> PublicTemplates { get; set; } = new();

    // Danh sách thực đơn cá nhân đã lưu của User
    public List<MealPlanTemplateSummaryDto> SavedUserPlans { get; set; } = new();

    // Thông số để tạo thực đơn mới
    public GeneratePlanInputModel GenerateInput { get; set; } = new();
}

public class GeneratePlanInputModel
{
    [Range(800, 6000, ErrorMessage = "Mức calo mục tiêu phải từ 800 đến 6.000 kcal.")]
    public int TargetCalories { get; set; } = 2000;

    [Range(3, 4, ErrorMessage = "Số bữa ăn phải là 3 hoặc 4 bữa.")]
    public int NumberOfMeals { get; set; } = 4;

    public GoalType GoalType { get; set; } = GoalType.Maintain;
    public DietType DietType { get; set; } = DietType.Normal;
}

public class MealPlanResult
{
    public int MealPlanId { get; set; } // 0 nếu chưa lưu
    public string Name { get; set; } = "Thực đơn Gợi ý Cá nhân hóa";
    public GoalType GoalType { get; set; }
    public string GoalTypeName { get; set; } = "Duy trì cân nặng";
    public int TargetCalories { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarb { get; set; }
    public decimal TotalFat { get; set; }
    public decimal TotalFiber { get; set; }
    public decimal TotalSugar { get; set; }
    public decimal TotalSodium { get; set; }

    public int ProteinPercent { get; set; }
    public int CarbPercent { get; set; }
    public int FatPercent { get; set; }

    public decimal CalorieDifferencePercent { get; set; }
    public bool IsWithinTarget { get; set; } = true;

    public string Explanation { get; set; } = string.Empty;
    public MacroRecommendation? RecommendedMacros { get; set; }

    public List<MealPlanMealDto> Meals { get; set; } = new();

    public List<string> AggregatedIngredients { get; set; } = new();
}

public class MealPlanMealDto
{
    public MealType MealType { get; set; }
    public string MealName { get; set; } = string.Empty;
    public string MealIcon { get; set; } = "bi-egg-fried";
    public int TargetCalories { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarb { get; set; }
    public decimal TotalFat { get; set; }

    public List<MealPlanItemDto> Items { get; set; } = new();
}

public class MealPlanItemDto
{
    public int FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Servings { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public decimal StandardServingSize { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carb { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public decimal Sodium { get; set; }
    public string? AllergenNotes { get; set; }
    public string? Ingredients { get; set; }
}

public class FoodSwapCandidate
{
    public int FoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal ServingSize { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carb { get; set; }
    public decimal Fat { get; set; }
    public decimal CalorieDiffKcal { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public string? Ingredients { get; set; }
    public string? AllergenNotes { get; set; }
    public decimal Sodium { get; set; }
    public decimal CalorieDiffPercent { get; set; }
}

public class MealPlanTemplateSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GoalType GoalType { get; set; }
    public string GoalTypeName { get; set; } = string.Empty;
    public int TargetCalories { get; set; }
    public bool IsTemplate { get; set; }
    public bool IsPublic { get; set; }
    public string? Explanation { get; set; }
    public int TotalFoodsCount { get; set; }
    public Food? PreviewFood { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SwapFoodRequestModel
{
    [Required]
    public int OldFoodId { get; set; }

    [Required]
    public int NewFoodId { get; set; }

    [Required]
    public int MealType { get; set; } // 1: Breakfast, 2: Lunch, 3: Dinner, 4: Snack
}
