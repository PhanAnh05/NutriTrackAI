using NutriTrackAI.Services;
using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class MealLogIndexViewModel
{
    [DataType(DataType.Date)]
    public DateTime SelectedDate { get; set; } = GoalPolicy.Today;

    // 4 Bữa ăn trong ngày
    public MealLog? BreakfastLog { get; set; }
    public MealLog? LunchLog { get; set; }
    public MealLog? DinnerLog { get; set; }
    public MealLog? SnackLog { get; set; }

    // Dinh dưỡng toàn ngày
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarb { get; set; }
    public decimal TotalFat { get; set; }
    public decimal TotalFiber { get; set; }
    public decimal TotalSugar { get; set; }
    public decimal TotalSodium { get; set; }
    public NutriTrackAI.Services.DietaryConstraints DietaryConstraints { get; set; } = NutriTrackAI.Services.DietaryConstraints.None;
    public NutriTrackAI.Services.MacroRecommendation? RecommendedMacros { get; set; }

    // Ngân sách & Cân bằng calo
    public int DailyCalorieTarget { get; set; } = 2000;
    public decimal CaloriesBurnedToday { get; set; }
    public decimal ExerciseCreditApplied { get; set; }
    public decimal RemainingCalories { get; set; }
    public string CalorieStatusClass { get; set; } = "text-success"; // text-success, text-warning, text-danger
    public bool UsesMaintenanceReference { get; set; }
    public string CalorieReferenceLabel { get; set; } = CalorieBudget.PlanLabel;
    public string CalorieStatusBadgeBg { get; set; } = "bg-success";

    // Danh sách cảnh báo dị ứng phát hiện trong các món của ngày
    public List<string> AllergyAlerts { get; set; } = new();

    // Món yêu thích để chọn nhanh
    public List<Food> FavoriteFoods { get; set; } = new();

    // Toàn bộ danh mục món ăn (dùng cho modal tìm kiếm thêm món)
    public List<FoodCategory> Categories { get; set; } = new();
}

public class AddMealFoodInputModel
{
    [Required]
    public int MealType { get; set; } // 1: Breakfast, 2: Lunch, 3: Dinner, 4: Snack

    [Required(ErrorMessage = "Vui lòng chọn món ăn.")]
    public int FoodId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập định lượng.")]
    [Range(0.01, 5000, ErrorMessage = "Định lượng phải lớn hơn 0 và không vượt quá 5.000.")]
    public decimal Servings { get; set; } = 100m;

    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; } = GoalPolicy.Today;

    public bool ConfirmedAllergy { get; set; } = false;
}

public class UpdateMealDetailInputModel
{
    [Required]
    public int DetailId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập định lượng mới.")]
    [Range(0.01, 5000, ErrorMessage = "Định lượng phải lớn hơn 0 và không vượt quá 5.000.")]
    public decimal Servings { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; }
}
