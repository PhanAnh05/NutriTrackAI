using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

public class GoalTimingTests
{
    [Fact]
    public void LossOptions_UseWholeWeeksAndMatchFullBudget()
    {
        var plan = GoalPlanningService.AnalyzeEnergy(2786.125m, GoalType.Lose, Gender.Male, 85, 80, 10, 170);
        Assert.Collection(plan.PaceOptions,
            option => Check(option, "gentle", 20, 275m, 2511),
            option =>
            {
                Check(option, "standard", 10, 550m, 2236);
                Assert.True(option.IsRecommended);
            },
            option => Check(option, "quick", 7, 785.71m, 2000),
            option =>
            {
                Check(option, "fastest", 6, 916.67m, 1869);
                Assert.True(option.RequiresIntensePaceAck);
                Assert.Equal("Mạnh", option.PaceZoneLabel);
            });
        var introductory = GoalPlanningService.AnalyzeEnergy(2786.125m, GoalType.Lose, Gender.Male, 85, 80, 10, 170, true);
        Assert.Equal(2536, introductory.IntroductoryDailyBudgetKcal);
        Assert.Equal(plan.PaceOptions, introductory.PaceOptions);
    }

    [Fact]
    public void DoctorAwareLossOptions_OfferTheFastestAllowedDuration()
    {
        var health = new HealthAssessment(HealthTier.NeedsDoctor, [], false, null, null);
        var plan = GoalPlanningService.AnalyzeEnergy(2786.125m, GoalType.Lose, Gender.Male, 85, 80, 20, 170, health: health);
        Assert.Collection(plan.PaceOptions,
            option => Check(option, "gentle", 20, 275m, 2511),
            option => Unavailable(option, 10, "Vượt giới hạn an toàn"),
            option => Unavailable(option, 7, "Vượt giới hạn an toàn"),
            option => Check(option, "fastest", 16, 343.75m, 2442));
    }

    [Fact]
    public void GainOptions_MergeTheRecommendedDurationWithFastest()
    {
        var plan = GoalPlanningService.AnalyzeEnergy(1641.75m, GoalType.Gain, Gender.Female, 48, 52, 14, 160);
        Assert.Collection(plan.PaceOptions,
            option => Check(option, "gentle", 20, 220m, 1862),
            option =>
            {
                Check(option, "fastest", 14, 314.29m, 1956);
                Assert.True(option.IsRecommended);
            },
            option => Unavailable(option, 10, "Vượt giới hạn an toàn"));
    }

    [Fact]
    public void LongTimelinesAndUnsafeBmi_DoNotOfferInvalidDurations()
    {
        var longPlan = GoalPlanningService.AnalyzeEnergy(3000m, GoalType.Lose, Gender.Male, 100, 70, 60, 170);
        Unavailable(Assert.Single(longPlan.PaceOptions, option => option.Key == "gentle"), 120, "Vượt thời gian hỗ trợ tối đa 104 tuần");
        var unsafeBmi = GoalPlanningService.AnalyzeEnergy(2786.125m, GoalType.Gain, Gender.Male, 85, 90, 10, 170);
        Assert.True(unsafeBmi.IsBlocked);
        Assert.True(unsafeBmi.HideEnergyDetails);
        Assert.Empty(unsafeBmi.PaceOptions);
    }

    private static void Check(PaceOption option, string key, int weeks, decimal adjustment, int budget)
    {
        Assert.True(option.IsAvailable);
        Assert.Equal(key, option.Key);
        Assert.Equal(weeks, option.Weeks);
        Assert.Equal(GoalPolicy.Today.AddDays(weeks * 7), option.TargetDate);
        Assert.Equal(adjustment, Math.Round(option.DailyAdjustmentKcal, 2));
        Assert.Equal(budget, option.DailyBudgetKcal);
    }

    private static void Unavailable(PaceOption option, int weeks, string reason)
    {
        Assert.False(option.IsAvailable);
        Assert.Equal(weeks, option.Weeks);
        Assert.Null(option.DailyBudgetKcal);
        Assert.Contains(reason, option.UnavailableReason);
    }
}
