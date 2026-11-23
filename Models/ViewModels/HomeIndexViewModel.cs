using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class HomeIndexViewModel
{
    public List<Article> Faqs { get; set; } = new();
    public List<HomeMealPlanPreview> PublicMealPlans { get; set; } = new();
    public int TotalFoods { get; set; }
    public int TotalExercises { get; set; }
}

public sealed record HomeMealPlanPreview(
    int Id, string Name, GoalType GoalType, int TargetCalories, string? Description, int FoodCount, HomeMealPlanPhoto? FirstFood);

public sealed record HomeMealPlanPhoto(string Name, string? ImageUrl);
