using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

public class ExerciseCreditTests
{
    [Theory]
    [InlineData(-400, 0, 300)]
    [InlineData(0, 0, 300)]
    [InlineData(1, 1, 301)]
    [InlineData(400, 200, 500)]
    [InlineData(800, 300, 600)]
    public void RemainingCaloriesUsesRoundedCappedExerciseCredit(int burned, int credit, int remaining)
    {
        Assert.Equal((decimal)credit, GoalPolicy.ExerciseCredit(burned));
        Assert.Equal((decimal)remaining, GoalPolicy.RemainingCalories(1800, 1500m, burned));
    }

    [Theory]
    [InlineData(2200, 500)]
    [InlineData(2300, null)]
    public void ReviewComparesLoggedIntakeUsingOnlyTheExerciseCredit(int intake, int? proposedAdjustment)
    {
        var today = new DateTime(2026, 11, 9);
        var goal = new Goal { GoalType = GoalType.Lose, TargetWeight = 65m, StartDate = today.AddDays(-14) };
        var budget = new CalorieBudget(goal, 2050m, 250m, 1800, "Làm quen", "", null, today, false, MaximumAdjustment: 512.5m);
        var state = new CaloriePlanState { Adjustment = 250m, Introductory = true, LastReviewedOn = goal.StartDate };
        var dates = Enumerable.Range(0, 14).Select(day => today.AddDays(-day)).ToArray();
        var weights = dates.Select(date => new WeightLog { LoggedOn = date, WeightKg = 70m });
        var meals = dates.Select(date => new MealDay(date, intake, 400m)).ToArray();

        var review = AdaptiveCalorieService.Evaluate(budget, state, weights, meals, today);

        Assert.Equal((decimal?)proposedAdjustment, review.ProposedAdjustment);
    }
}
