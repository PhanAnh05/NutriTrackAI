using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

public class Phase3PolicyTests
{
    private static readonly DateTime Today = new(2026, 11, 17);
    private readonly NutritionCalculatorService calculator = new();

    public static TheoryData<int, int, PaceZone, int> Deadlines => new()
    {
        { 0, 5, PaceZone.OverLimit, 0 }, { 0, 7, PaceZone.Standard, 1645 },
        { 0, 10, PaceZone.Standard, 1880 }, { 0, 14, PaceZone.Gentle, 2037 },
        { 0, 20, PaceZone.Gentle, 2155 }, { 0, 30, PaceZone.VerySlow, 2247 },
        { 1, 12, PaceZone.OverLimit, 0 }, { 1, 13, PaceZone.Intense, 1593 },
        { 1, 14, PaceZone.Standard, 1653 }, { 1, 20, PaceZone.Standard, 1889 },
        { 1, 42, PaceZone.VerySlow, 2177 },
        { 2, 13, PaceZone.Gentle, 1675 }, { 2, 9, PaceZone.Gentle, 1525 },
        { 2, 8, PaceZone.OverLimit, 0 },
        { 3, 12, PaceZone.Gentle, 1442 }, { 3, 8, PaceZone.OverLimit, 0 },
        { 4, 8, PaceZone.Standard, 2041 }
    };

    private static UserProfile Profile(int scenario = 0) => new()
    {
        Gender = scenario is 2 or 3 ? Gender.Female : Gender.Male,
        HeightCm = scenario is 2 or 3 ? 160 : scenario is 1 or 4 ? 175 : 170,
        CurrentWeightKg = scenario switch { 2 => 60, 3 => 55, 4 => 90, _ => 85 },
        DateOfBirth = new DateTime(scenario is 2 or 3 ? 1998 : scenario == 1 ? 1991 : 1996, 1, 1),
        ActivityLevel = scenario is 2 or 4 ? ActivityLevel.Moderate : ActivityLevel.Light,
        HealthScreeningJson = HealthScreening.Update(null, QaApplication.NoReportedFactors)
    };

    [Theory]
    [MemberData(nameof(Deadlines))]
    public void DeadlinesFollowTheBmiTier(int scenario, int weeks, PaceZone zone, int calories)
    {
        using var clock = GoalPolicy.OverrideUtcNow(Today);
        var target = scenario switch { 1 => 75, 2 => 56, 3 => 52, 4 => 84, _ => 80 };
        var result = new GoalPlanningService(calculator).Analyze(Profile(scenario), GoalType.Lose, target, weeks);
        Assert.Equal(zone, result.PaceZone);
        Assert.Equal(zone == PaceZone.Intense, result.RequiresIntensePaceAck);
        Assert.Equal(zone == PaceZone.OverLimit, result.IsBlocked);
        if (!result.IsBlocked) Assert.Equal(calories, result.DailyCalorieTarget);
        if (zone == PaceZone.VerySlow) Assert.Contains("cân dao động theo nước", result.WarningMessage);
        Assert.Equal(new[] { 7, 13, 9, 10, 7 }[scenario], result.MinimumMathematicalWeeks);
    }

    [Fact]
    public void ActiveDeficitUsesStartBmiAndToleranceWhilePreservingTheCalorieFloor()
    {
        using var clock = GoalPolicy.OverrideUtcNow(Today);
        var profile = Profile();
        profile.CurrentWeightKg = 72;
        var planner = new GoalPlanningService(calculator);
        Assert.True(planner.CanApply(profile, GoalType.Lose, 70, 750, out _, startWeightKg: 85));
        Assert.False(planner.CanApply(profile, GoalType.Lose, 70, 750, out _));
        Assert.False(planner.CanApply(profile, GoalType.Lose, 70, 780, out _, startWeightKg: 85));

        profile.ActivityLevel = ActivityLevel.Moderate;
        Assert.True(planner.CanApply(profile, GoalType.Lose, 70, 850, out _, startWeightKg: 85));
        Assert.False(planner.CanApply(profile, GoalType.Lose, 70, 850, out _, startWeightKg: 85, allowTolerance: false));
        Assert.False(planner.CanApply(profile, GoalType.Lose, 70, 872, out _, startWeightKg: 85));
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void DoctorAwarePlanHasAHardCapForLossAndGain(int weeks, bool blocked)
    {
        using var clock = GoalPolicy.OverrideUtcNow(Today);
        var profile = Profile();
        profile.MedicalConditions = MedicalConditions.Hypertension;
        profile.DoctorAwareOfPlan = true;
        profile.HealthScreeningAt = GoalPolicy.Now;
        var planner = new GoalPlanningService(calculator);
        var plan = planner.Analyze(profile, GoalType.Lose, 80, weeks);
        Assert.Equal(HealthTier.NeedsDoctor, plan.Health!.Tier);
        Assert.Equal(blocked, plan.IsBlocked);
        Assert.Equal(16, plan.MinimumMathematicalWeeks);
        Assert.True(Assert.Single(plan.PaceOptions, option => option.Key == "fastest").IsAvailable);
        Assert.False(Assert.Single(plan.PaceOptions, option => option.Key == "quick").IsAvailable);
        if (!blocked)
        {
            Assert.Equal(2087, plan.DailyCalorieTarget);
            Assert.Equal(.3125m, plan.WeeklyRateKg);
            Assert.Equal(PaceZone.Gentle, plan.PaceZone);
        }
        Assert.False(planner.CanApply(profile, GoalType.Lose, 80, 351, out _, startWeightKg: 85));
        profile.CurrentWeightKg = 67;
        Assert.True(planner.CanApply(profile, GoalType.Gain, 70, 350, out _));
        Assert.False(planner.CanApply(profile, GoalType.Gain, 70, 351, out _));
    }

    [Theory]
    [InlineData(MedicalConditions.KidneyDisease)]
    [InlineData(MedicalConditions.PregnantOrBreastfeeding)]
    [InlineData(MedicalConditions.OtherCondition)]
    [InlineData(MedicalConditions.EatingDisorderHistory)]
    [InlineData(MedicalConditions.UsesInsulinOrGlucoseLowering)]
    public void BlockedConditionWinsOverOtherConditionsAndDoctorAcknowledgment(MedicalConditions condition)
    {
        var profile = Profile();
        profile.MedicalConditions = condition | MedicalConditions.Hypertension | MedicalConditions.Gastritis;
        profile.DoctorAwareOfPlan = true;
        profile.HealthScreeningAt = GoalPolicy.Now;
        var assessment = GoalPolicy.EvaluateHealth(profile);
        Assert.Equal(HealthTier.Blocked, assessment.Tier);
        Assert.Equal(4, assessment.Reasons.Count);
        var plan = new GoalPlanningService(calculator).Analyze(profile, GoalType.Maintain, 85, 4);
        Assert.True(plan.IsBlocked);
        Assert.Equal(0, plan.DailyCalorieTarget);
    }

    [Fact]
    public void NewScreeningOverridesLegacyNotesAndCautionDoesNotChangeTdee()
    {
        using var clock = GoalPolicy.OverrideUtcNow(Today);
        var profile = Profile();
        var planner = new GoalPlanningService(calculator);
        Assert.True(GoalPolicy.EvaluateHealth(profile).NeedsScreening);
        Assert.False(planner.Analyze(profile, GoalType.Lose, 80, 7).IsBlocked);
        profile.HealthNotes = "mang thai";
        Assert.Equal(GoalPolicy.CompleteHealthScreening, planner.Analyze(profile, GoalType.Lose, 80, 7).BlockedReason);
        profile.HealthScreeningAt = GoalPolicy.Now;
        foreach (var condition in new[] { MedicalConditions.Gastritis, MedicalConditions.Hypothyroidism })
        {
            profile.MedicalConditions = condition;
            var plan = planner.Analyze(profile, GoalType.Lose, 80, 7);
            Assert.Equal(HealthTier.Caution, plan.Health!.Tier);
            Assert.Equal(2430.3125m, plan.Tdee);
            Assert.Equal(.77m, plan.MaxWeeklyRateKg);
            Assert.False(plan.IsBlocked);
        }
        profile.MedicalConditions = MedicalConditions.Hypertension;
        Assert.Contains("Cần bác sĩ xác nhận", planner.Analyze(profile, GoalType.Lose, 80, 16).BlockedReason);
    }

    [Theory]
    [InlineData(MedicalConditions.Gout, 30, 40, 30, 102.0, 263.2, 69.6)]
    [InlineData(MedicalConditions.Diabetes, 30, 40, 30, 156.5, 208.7, 69.6)]
    [InlineData(MedicalConditions.Diabetes, 25, 50, 25, 143.5, 234.8, 63.8)]
    [InlineData(MedicalConditions.None, 30, 25, 45, 156.5, 182.6, 81.2)]
    public void ConditionMacrosPreserveEnergyAndMatchTheDisplayedGrams(MedicalConditions conditions,
        int proteinRatio, int carbRatio, int fatRatio, double protein, double carbs, double fat)
    {
        var profile = Profile();
        profile.HealthScreeningAt = GoalPolicy.Now;
        profile.MedicalConditions = conditions;
        var constraints = GoalPolicy.EvaluateHealth(profile).Constraints;
        var macros = GoalPolicy.ApplyConditionMacros(2087, 85, (proteinRatio / 100m, carbRatio / 100m, fatRatio / 100m), constraints);
        Assert.Null(macros.UnavailableReason);
        Assert.Equal((decimal)protein, Math.Round(macros.ProteinGrams, 1));
        Assert.Equal((decimal)carbs, Math.Round(macros.CarbGrams, 1));
        Assert.Equal((decimal)fat, Math.Round(macros.FatGrams, 1));
        Assert.InRange(macros.ProteinCalories + macros.CarbCalories + macros.FatCalories, 2086.9999m, 2087.0001m);
        Assert.InRange(macros.FatCalories, 0, 2087 * GoalPolicy.MaxFatRatio + .0001m);
        Assert.True(constraints.RequireMedicalDisclaimer);
    }

    [Fact]
    public void GoutAndDiabetesUseBothCapsAndReportWhenThereIsNoFeasibleSplit()
    {
        var profile = Profile();
        profile.HealthScreeningAt = GoalPolicy.Now;
        profile.MedicalConditions = MedicalConditions.Gout | MedicalConditions.Diabetes | MedicalConditions.HeartDisease;
        var constraints = GoalPolicy.EvaluateHealth(profile).Constraints;
        Assert.True(constraints.NoKeto);
        Assert.True(constraints.DistributeCarbs);
        Assert.True(constraints.WaterPerDoctor);
        var macros = GoalPolicy.ApplyConditionMacros(2000, 85, (.3m, .4m, .3m), constraints);
        Assert.Null(macros.UnavailableReason);
        Assert.Equal(102m, macros.ProteinGrams);
        Assert.Equal(225m, macros.CarbGrams);
        Assert.Equal(692m, macros.FatCalories, 8);
        Assert.Equal(2000m, macros.ProteinCalories + macros.CarbCalories + macros.FatCalories, 8);
        var incompatible = GoalPolicy.ApplyConditionMacros(2087, 85, (.3m, .4m, .3m), constraints);
        Assert.Equal(GoalPolicy.IncompatibleMacros, incompatible.UnavailableReason);
    }

    [Fact]
    public void PromptUsesTranslatedConstraintsAndOneDisclaimerEvenWhenPlanIsBlocked()
    {
        var profile = Profile();
        profile.HealthScreeningAt = GoalPolicy.Now;
        profile.MedicalConditions = MedicalConditions.Gastritis | MedicalConditions.Diabetes |
            MedicalConditions.Gout | MedicalConditions.Hypothyroidism | MedicalConditions.HeartDisease | MedicalConditions.KidneyDisease;
        var assessment = GoalPolicy.EvaluateHealth(profile);
        var context = new UserNutritionContext { Constraints = assessment.Constraints, PlanSuspended = true };
        var prompt = AiPromptBuilder.Build(context, UserContextBuilder.FormatSystemData(context));
        foreach (var tag in new[]
                 {
                     "ít natri", "chỉ số đường huyết thấp", "hạn chế thực phẩm giàu purine", "tránh đồ chua cay",
                     "ít nhất 4 bữa mỗi ngày", "không gợi ý nhịn ăn gián đoạn", "không gợi ý chế độ keto",
                     "chia tinh bột đều", "lượng nước theo hướng dẫn bác sĩ", "ăn đậu nành cách giờ uống thuốc"
                 })
            Assert.Contains(tag, prompt);
        Assert.Contains(AiPromptBuilder.PlanSuspendedInstruction, prompt);
        Assert.DoesNotContain("Gout hoặc", prompt);
        Assert.DoesNotContain("Suy giáp", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Single(prompt.Split(GoalPolicy.MedicalDisclaimer).Skip(1));
        Assert.Contains("RÀNG BUỘC ĂN UỐNG BẮT BUỘC\nkhông có", AiPromptBuilder.Build(new(), "").ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
