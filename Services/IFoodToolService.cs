using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public interface IFoodToolService
{
    /// <summary>
    /// Hàm 1: Lấy tổng hợp dinh dưỡng và nhật ký ăn uống hôm nay của người dùng
    /// </summary>
    Task<TodayNutritionDto> GetTodayNutritionAsync(int userId, DateTime? date = null);

    /// <summary>
    /// Hàm 2: Lấy số calo còn lại hôm nay theo phương trình cân bằng năng lượng
    /// </summary>
    Task<RemainingCaloriesDto> GetRemainingCaloriesAsync(int userId, DateTime? date = null);

    Task<string?> FindMentionedFoodAsync(string message);

    /// <summary>
    /// Hàm 3: Tìm kiếm món ăn trong CSDL hệ thống theo từ khóa, mức calo tối đa, loại trừ dị ứng
    /// </summary>
    Task<List<FoodSearchResultDto>> SearchFoodsAsync(
        string? keyword,
        decimal? maxKcal = null,
        List<string>? excludeAllergens = null,
        bool vegetarianOnly = false,
        List<string>? dislikedFoods = null);

    /// <summary>
    /// Hàm 4: Gợi ý bữa ăn (2-3 món) trong giới hạn calo cho phép
    /// </summary>
    Task<MealSuggestionDto> SuggestMealAsync(
        decimal maxKcal,
        DietType dietType,
        List<string>? excludeAllergens = null,
        bool preferLowSodium = false,
        List<string>? dislikedFoods = null);

    /// <summary>
    /// Hàm 5: Gợi ý món thay thế tương đương dinh dưỡng khi người dùng hỏi "Thay món X bằng món gì?"
    /// </summary>
    Task<SwapSuggestionDto> GetSwapSuggestionsAsync(
        string foodName,
        DietType dietType,
        List<string>? excludeAllergens = null,
        bool preferLowSodium = false,
        List<string>? dislikedFoods = null);
}

public class TodayNutritionDto
{
    public DateTime Date { get; set; }
    public decimal DailyCalorieTarget { get; set; }
    public decimal CaloriesConsumed { get; set; }
    public decimal CaloriesBurned { get; set; }
    public decimal ExerciseCreditApplied { get; set; }
    public decimal RemainingCalories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carb { get; set; }
    public decimal Fat { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public decimal Sodium { get; set; }
    public int? MaxSodiumMg { get; set; }
    public MacroRecommendation? RecommendedMacros { get; set; }
    public int WaterMl { get; set; }
    public List<string> MealsLogged { get; set; } = new();
}

public class RemainingCaloriesDto
{
    public DateTime Date { get; set; }
    public decimal DailyTarget { get; set; }
    public decimal Consumed { get; set; }
    public decimal Burned { get; set; }
    public decimal ExerciseCreditApplied { get; set; }
    public decimal Remaining { get; set; }
    public string Status { get; set; } = "green"; // green, yellow, red
    public string Message { get; set; } = string.Empty;
}

public class FoodSearchResultDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal ServingSize { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carb { get; set; }
    public decimal Fat { get; set; }
    public decimal Sodium { get; set; }
    public decimal Fiber { get; set; }
    public decimal Sugar { get; set; }
    public string? CookingState { get; set; }
    public string? Ingredients { get; set; }
    public string? HealthWarning { get; set; }
    public string? AllergenNotes { get; set; }
}

public class MealSuggestionDto
{
    public decimal TargetMaxKcal { get; set; }
    public decimal TotalKcal { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarb { get; set; }
    public decimal TotalFat { get; set; }
    public List<FoodSearchResultDto> SuggestedItems { get; set; } = new();
    public string Advice { get; set; } = string.Empty;
}

public class SwapSuggestionDto
{
    public string OriginalFoodName { get; set; } = string.Empty;
    public bool FoundOriginalFood { get; set; }
    public decimal OriginalCalories { get; set; }
    public decimal OriginalProtein { get; set; }
    public List<FoodSearchResultDto> AlternativeFoods { get; set; } = new();
    public string Note { get; set; } = string.Empty;
}
