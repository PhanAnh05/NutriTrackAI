using NutriTrackAI.Services;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Models.ViewModels;

public class FoodIndexViewModel
{
    public List<Food> Foods { get; set; } = new();
    public List<FoodCategory> Categories { get; set; } = new();

    // Bộ lọc
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinCal { get; set; }
    public decimal? MaxCal { get; set; }
    public bool? IsVegetarian { get; set; }

    // Phân trang
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }

    // Danh sách FoodId yêu thích của User hiện tại
    public HashSet<int> FavoriteFoodIds { get; set; } = new();
}

public class FoodDetailsViewModel
{
    public Food Food { get; set; } = null!;
    public bool IsFavorite { get; set; }
    public bool HasAllergyWarning { get; set; }
    public string? AllergensFound { get; set; }
    public string? UserAllergies { get; set; }
    public decimal UserDailyTargetCalories { get; set; }
}

public class QuickAddMealModel
{
    public int FoodId { get; set; }
    public int MealType { get; set; } = 1; // 1: Breakfast, 2: Lunch, 3: Dinner, 4: Snack
    public decimal Servings { get; set; } = 100m;
    public DateTime Date { get; set; } = GoalPolicy.Today;
    public bool ConfirmedAllergy { get; set; } = false;
}
