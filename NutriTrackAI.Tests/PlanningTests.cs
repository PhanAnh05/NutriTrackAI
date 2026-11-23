using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;
using Xunit;

namespace NutriTrackAI.Tests;

public class PlanningTests
{
    private readonly NutritionCalculatorService calculator = new();
    private UserProfile Profile(decimal weight = 72, Gender gender = Gender.Male) => new()
    {
        UserId = 1, DateOfBirth = GoalPolicy.Today.AddYears(-24), CurrentWeightKg = weight,
        HeightCm = 170, Gender = gender, ActivityLevel = ActivityLevel.Moderate, TargetWeightKg = 65,
        HealthScreeningJson = HealthScreening.Update(null, QaApplication.NoReportedFactors)
    };
    private CalorieBudgetService Budgets() => new(new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options), calculator, new(calculator));
    private Goal Goal(decimal adjustment = 500) => new() { UserId = 1, GoalType = GoalType.Lose,
        StartWeight = 72, TargetWeight = 65, StartDate = GoalPolicy.Today.AddDays(-40),
        TargetDate = GoalPolicy.Today.AddDays(30), CaloriePlanJson = new CaloriePlanState {
            Adjustment = adjustment, LastReviewedOn = GoalPolicy.Today.AddDays(-28) }.ToJson() };

    [Fact] public void BMI_72_170() => Assert.InRange(calculator.CalculateBmi(72, 170), 24.91m, 24.92m);
    [Theory]
    [InlineData(184, "Thiếu cân")]
    [InlineData(185, "Bình thường")]
    [InlineData(229, "Bình thường")]
    [InlineData(230, "Thừa cân")]
    [InlineData(249, "Thừa cân")]
    [InlineData(250, "Béo phì")]
    public void AdultBmi_UsesTheVietnamScreeningThresholds(int bmiInTenths, string expected)
        => Assert.Equal(expected, calculator.GetBmiCategory(bmiInTenths / 10m));
    [Fact]
    public void BmiWeightLimits_AreConsistentInCalculatorAndAnalysis()
    {
        Assert.Equal("Thừa cân", calculator.GetBmiInfo(65m, 165m, 24).Category);
        calculator.GetBmiWeightLimits(170m, out var min, out var maxNormal, out var maxGain);
        var analysis = new GoalPlanningService(calculator).Analyze(Profile(), GoalType.Lose, 65m, 16);

        Assert.Equal(53.5m, min);
        Assert.Equal(66.1m, maxNormal);
        Assert.Equal(71.9m, maxGain);
        Assert.Equal((min, maxNormal, maxGain), (analysis.MinSafeWeight, analysis.MaxNormalWeight, analysis.MaxAllowedWeight));
    }
    [Fact] public void BMR_Male24() => Assert.Equal(1667.5m, calculator.CalculateBmr(Gender.Male, 72, 170, 24));
    [Fact] public void TDEE_PreservesPrecision() => Assert.Equal(2584.625m, calculator.CalculateTdee(1667.5m, ActivityLevel.Moderate));
    [Fact] public void Age_UsesBirthday()
    {
        Assert.Equal(24, calculator.CalculateAge(GoalPolicy.Today.AddYears(-24)));
        Assert.Equal(23, calculator.CalculateAge(GoalPolicy.Today.AddYears(-24).AddDays(1)));
    }
    [Fact] public void OtherGender_ProjectConvention() => Assert.Equal(1584.5m, calculator.CalculateBmr(Gender.Other, 72, 170, 24));
    [Theory]
    [InlineData(9, 855.55556, true)] [InlineData(11, 700, true)]
    [InlineData(14, 550, false)] [InlineData(16, 481.25, false)]
    public void DeadlineEnergy(int weeks, double expected, bool blocked)
    {
        var result = new GoalPlanningService(calculator).Analyze(Profile(), GoalType.Lose, 65, weeks);
        Assert.InRange(result.RequiredDailyAdjustment, (decimal)expected - .001m, (decimal)expected + .001m);
        Assert.Equal(blocked, result.IsBlocked);
        if (weeks == 9) { Assert.Equal(GoalSafetyLevel.Dangerous, result.SafetyLevel); Assert.Equal(12, result.MinimumMathematicalWeeks); }
        Assert.True(result.DailyCalorieTarget >= 1500);
    }
    [Fact]
    public void PersonalizedEnergyLimits_RespectEachCapAndTheCalorieFloor()
    {
        Assert.Equal(450m, GoalPolicy.MaxDeficit(1800m, 72m, 170m));
        Assert.Equal(500m, GoalPolicy.MaxDeficit(2600m, 50m, 165m));
        Assert.Equal(990m, GoalPolicy.MaxDeficit(3500m, 90m, 175m));
        Assert.Equal(360m, GoalPolicy.MaxSurplus(1800m));
        Assert.Equal(500m, GoalPolicy.MaxSurplus(3000m));
        Assert.Equal(300m, GoalPolicy.MaximumAdjustment(GoalType.Lose, 1800m, Gender.Male, 72m, 170m));
        Assert.Equal(0m, GoalPolicy.MaximumAdjustment(GoalType.Lose, 1500m, Gender.Male, 72m, 170m));
    }
    [Fact]
    public void PersonalizedEnergyLimits_AcceptThirteenWeeksAndRejectNineWeekDeadline()
    {
        var planner = new GoalPlanningService(calculator);
        var woman = Profile(60m, Gender.Female);
        woman.HeightCm = 160m;
        woman.DateOfBirth = GoalPolicy.Today.AddYears(-28);
        var accepted = planner.Analyze(woman, GoalType.Lose, 56m, 13);
        Assert.False(accepted.IsBlocked);
        Assert.Equal(2013.45m, accepted.Tdee);
        Assert.InRange(accepted.RequiredDailyAdjustment, 338.46m, 338.47m);
        Assert.Equal(1675, accepted.DailyCalorieTarget);

        var man = Profile(85m);
        man.HeightCm = 175m;
        man.DateOfBirth = GoalPolicy.Today.AddYears(-35);
        man.ActivityLevel = ActivityLevel.Light;
        var rejected = planner.Analyze(man, GoalType.Lose, 75m, 9);
        Assert.True(rejected.IsBlocked);
        Assert.Equal(2438.90625m, rejected.Tdee);
        Assert.InRange(rejected.RequiredDailyAdjustment, 1222.22m, 1222.23m);
        Assert.Equal(853.6171875m, GoalPolicy.MaxDeficit(rejected.Tdee, man.CurrentWeightKg, man.HeightCm));
        Assert.Equal(13, rejected.MinimumMathematicalWeeks);
        Assert.Equal(20, rejected.RecommendedWeeks);
    }

    [Fact]
    public void LossRecommendationStartsAtHalfKgPerWeekWhenProfileAllows()
    {
        var plan = new GoalPlanningService(calculator).Analyze(Profile(85m), GoalType.Lose, 80m, 10);
        Assert.False(plan.IsBlocked);
        Assert.Equal(10, plan.RecommendedWeeks);
        Assert.Equal(10, plan.EstimatedWeeks);
        Assert.Equal(550m, plan.PlannedDailyAdjustment);
        Assert.Equal(0.5m, GoalPolicy.WeeklyRate(plan.PlannedDailyAdjustment));

        var floorLimited = GoalPlanningService.AnalyzeEnergy(1800m, GoalType.Lose, Gender.Male, 85m, 80m, 19, 175m);
        Assert.False(floorLimited.IsBlocked);
        Assert.Equal(19, floorLimited.RecommendedWeeks);
        Assert.Equal(5500m / 19m, floorLimited.PlannedDailyAdjustment);
        Assert.Equal(1511, floorLimited.DailyCalorieTarget);
    }

    [Theory]
    [InlineData(21, PaceZone.VerySlow, false)]
    [InlineData(20, PaceZone.Gentle, false)]
    [InlineData(11, PaceZone.Gentle, false)]
    [InlineData(10, PaceZone.Standard, false)]
    [InlineData(9, PaceZone.Standard, false)]
    [InlineData(7, PaceZone.Standard, false)]
    [InlineData(6, PaceZone.Intense, true)]
    [InlineData(5, PaceZone.OverLimit, false)]
    public void LossPaceZones_UseExactRatesAndProfileCap(int weeks, PaceZone zone, bool acknowledge)
    {
        var plan = GoalPlanningService.AnalyzeEnergy(3200m, GoalType.Lose, Gender.Male, 90m, 85m, weeks, 175m);

        Assert.Equal(zone, plan.PaceZone);
        Assert.Equal(acknowledge, plan.RequiresIntensePaceAck);
        Assert.Equal(zone == PaceZone.OverLimit, plan.IsBlocked);
        Assert.Equal(.90m, plan.MaxWeeklyRateKg);
        if (!plan.IsBlocked)
        {
            Assert.Equal(5500m / weeks, plan.PlannedDailyAdjustment);
            Assert.InRange(plan.AppliedWeeklyRateKg, 5m / weeks - .000001m, 5m / weeks + .000001m);
        }
    }

    [Fact]
    public void FastestPaceOption_UsesTheShortestAllowedWholeWeekDuration()
    {
        var plan = GoalPlanningService.AnalyzeEnergy(3200m, GoalType.Lose, Gender.Male, 90m, 85m, 7, 175m);
        var options = plan.PaceOptions.ToDictionary(option => option.Key);

        Assert.Equal(20, options["gentle"].Weeks);
        Assert.Equal(.25m, options["gentle"].WeeklyRateKg);
        Assert.Equal(10, options["standard"].Weeks);
        Assert.Equal(.5m, options["standard"].WeeklyRateKg);
        Assert.Equal(7, options["quick"].Weeks);
        Assert.Equal(6, options["fastest"].Weeks);
        Assert.InRange(options["fastest"].WeeklyRateKg, .83m, .84m);
        Assert.True(options["fastest"].WeeklyRateKg < plan.MaxWeeklyRateKg);
    }

    [Fact]
    public void PaceOptions_RespectTheCalorieFloorAndMinimumDuration()
    {
        var floorLimited = GoalPlanningService.AnalyzeEnergy(1800m, GoalType.Lose, Gender.Male, 85m, 80m, 19, 175m);
        var fastest = Assert.Single(floorLimited.PaceOptions, option => option.Key == "fastest");
        Assert.Equal(19, fastest.Weeks);
        var standard = Assert.Single(floorLimited.PaceOptions, option => option.Key == "standard");
        Assert.Equal(10, standard.Weeks);
        Assert.False(standard.IsAvailable);
        Assert.NotNull(Assert.Single(floorLimited.PaceOptions, option => option.Key == "quick").UnavailableReason);
        Assert.Equal(.27m, floorLimited.MaxWeeklyRateKg);
        Assert.True(floorLimited.DailyCalorieTarget >= GoalPolicy.CalorieFloor(Gender.Male));
        var belowFloor = GoalPlanningService.AnalyzeEnergy(1800m, GoalType.Lose, Gender.Male, 85m, 80m, 18, 175m);
        Assert.True(belowFloor.IsBlocked);
        Assert.Equal(PaceZone.OverLimit, belowFloor.PaceZone);

        var shortGoal = GoalPlanningService.AnalyzeEnergy(3200m, GoalType.Lose, Gender.Male, 90m, 89m, 2, 175m);
        var fastestShortGoal = Assert.Single(shortGoal.PaceOptions, option => option.Weeks == 2);
        Assert.Equal("fastest", fastestShortGoal.Key);
        Assert.True(fastestShortGoal.IsRecommended);
    }

    [Theory]
    [InlineData(11, PaceZone.VerySlow)]
    [InlineData(10, PaceZone.Standard)]
    public void GainPace_UsesIndependentWarningThreshold(int weeks, PaceZone expected)
    {
        var plan = GoalPlanningService.AnalyzeEnergy(2400m, GoalType.Gain, Gender.Male, 65m, 66m, weeks, 175m);
        Assert.Equal(expected, plan.PaceZone);
        Assert.False(plan.RequiresIntensePaceAck);
        Assert.False(plan.IsBlocked);
    }

    [Fact]
    public void IntroductoryPace_ReportsTheAppliedRateSeparatelyFromTheRequestedRate()
    {
        var plan = GoalPlanningService.AnalyzeEnergy(3200m, GoalType.Lose, Gender.Male, 90m, 85m, 6, 175m, introductory: true);

        Assert.False(plan.IsBlocked);
        Assert.Equal(PaceZone.Intense, plan.PaceZone);
        Assert.True(plan.RequiresIntensePaceAck);
        Assert.Equal(5m / 6m, plan.WeeklyRateKg);
        Assert.Equal(250m, plan.PlannedDailyAdjustment);
        Assert.InRange(plan.AppliedWeeklyRateKg, .22m, .23m);
        Assert.Equal(2950, plan.DailyCalorieTarget);
    }

    [Fact]
    public void BodyCheckInCountsSevenDaysFromLatestResponseOrAdjustment()
    {
        var start = new DateTime(2026, 11, 1);
        var goal = Goal();
        goal.IsActive = true;
        goal.StartDate = start;
        var state = new CaloriePlanState();
        Assert.False(GoalPlanningService.IsBodyCheckInDue(goal, state, start.AddDays(6)));
        Assert.True(GoalPlanningService.IsBodyCheckInDue(goal, state, start.AddDays(7)));
        state.LastBodyCheckInOn = start.AddDays(7);
        Assert.False(GoalPlanningService.IsBodyCheckInDue(goal, state, start.AddDays(7)));
        state.Changes.Add(new(start.AddDays(10), 600m, "Điều chỉnh"));
        Assert.False(GoalPlanningService.IsBodyCheckInDue(goal, state, start.AddDays(16)));
        Assert.True(GoalPlanningService.IsBodyCheckInDue(goal, state, start.AddDays(17)));
    }

    [Fact]
    public void BodyAdjustmentCapsIncreaseAndAllowsDecreaseToZero()
    {
        var planner = new GoalPlanningService(calculator);
        var profile = Profile();
        var goal = Goal(550m);
        goal.IsActive = true;
        var budget = Budgets().Calculate(profile, goal);
        var increase = Assert.IsType<BodyAdjustmentPreview>(planner.PreviewBodyAdjustment(profile, goal, budget,
            BodyFeedback.Increase, GoalPolicy.Today, out _));
        Assert.Equal(budget.MaximumAdjustment, increase.Adjustment);
        Assert.True(increase.NewWeeklyRate > 0.5m);

        var state = CaloriePlanState.Read(goal.CaloriePlanJson)!;
        state.Adjustment = increase.Adjustment;
        goal.CaloriePlanJson = state.ToJson();
        Assert.Null(planner.PreviewBodyAdjustment(profile, goal, Budgets().Calculate(profile, goal),
            BodyFeedback.Increase, GoalPolicy.Today, out var reason));
        Assert.Contains("chạm trần", reason);

        state.Adjustment = 100m;
        goal.CaloriePlanJson = state.ToJson();
        var decrease = Assert.IsType<BodyAdjustmentPreview>(planner.PreviewBodyAdjustment(profile, goal,
            Budgets().Calculate(profile, goal), BodyFeedback.Decrease, GoalPolicy.Today, out _));
        Assert.Equal(0m, decrease.Adjustment);
        Assert.Null(decrease.NewTargetDate);
    }
    [Fact] public void WeightChange_RecalculatesTdee_WithoutIncreasingDeficit()
    {
        var service = Budgets(); var goal = Goal();
        var old = service.Calculate(Profile(), goal); var updated = service.Calculate(Profile(69), goal);
        Assert.Equal(2584.625m, old.Tdee); Assert.Equal(2538.125m, updated.Tdee);
        Assert.Equal(500m, updated.Adjustment); Assert.Equal(2038, updated.TargetCalories);
    }
    [Theory] [InlineData(600, 1938)] [InlineData(630, 1908)]
    public void ConfirmedAdjustment_ComputesNewTarget(int adjustment, int expected)
    {
        var profile = Profile(69); var planner = new GoalPlanningService(calculator);
        Assert.True(planner.CanApply(profile, GoalType.Lose, 65, adjustment, out _));
        Assert.Equal(expected, Budgets().Calculate(profile, Goal(adjustment)).TargetCalories);
    }
    [Theory] [InlineData(751)] [InlineData(-10)]
    public void InvalidDeficitRejected(int adjustment) => Assert.False(new GoalPlanningService(calculator).CanApply(Profile(), GoalType.Lose, 65, adjustment, out _));
    [Theory] [InlineData(Gender.Male, 55, 1500)] [InlineData(Gender.Female, 48, 1200)]
    public void CalorieFloors(Gender gender, int weight, int floor)
    {
        var p = Profile(weight, gender); p.HeightCm = 150; p.ActivityLevel = ActivityLevel.Sedentary;
        var tdee = calculator.CalculateTdee(calculator.CalculateBmr(gender, weight, 150, 24), p.ActivityLevel);
        var deficit = tdee - floor + 1;
        Assert.False(new GoalPlanningService(calculator).CanApply(p, GoalType.Lose, weight - 2, deficit, out _));
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(105)]
    public void InvalidWeeksRejected(int weeks) => Assert.True(new GoalPlanningService(calculator).Analyze(Profile(), GoalType.Lose, 65, weeks).IsBlocked);
    [Fact] public void Maintain_EqualsTdee()
    {
        var r = new GoalPlanningService(calculator).Analyze(Profile(), GoalType.Maintain, 72, 12);
        Assert.False(r.IsBlocked); Assert.Equal(0m, r.PlannedDailyAdjustment); Assert.Equal(2585, r.DailyCalorieTarget);
        Assert.Equal(PaceZone.None, r.PaceZone);
        Assert.Empty(r.PaceOptions);
        Assert.False(r.RequiresIntensePaceAck);
    }
    [Fact] public void Gain_HasIndependentLimits()
    {
        var planner = new GoalPlanningService(calculator); var profile = Profile(60);
        Assert.True(planner.Analyze(profile, GoalType.Gain, 65, 6).IsBlocked);
        var normal = planner.Analyze(profile, GoalType.Gain, 65, 16);
        Assert.False(normal.IsBlocked); Assert.InRange(normal.PlannedDailyAdjustment, 250, 500);
        Assert.True(normal.DailyCalorieTarget > normal.Tdee);
    }
    [Fact]
    public void GainPlans_EnforceTargetBmiAndSuspendAnExistingPlanAtBmi25()
    {
        var planner = new GoalPlanningService(calculator);
        var profile = Profile(62.5m);
        profile.HeightCm = 160m;
        Assert.False(planner.Analyze(profile, GoalType.Gain, 63.7m, 4).IsBlocked);
        Assert.True(planner.Analyze(profile, GoalType.Gain, 63.8m, 4).IsBlocked);
        Assert.False(planner.CanApply(profile, GoalType.Gain, 63.8m, 100m, out _));

        var goal = Goal(100m);
        goal.GoalType = GoalType.Gain;
        goal.StartWeight = profile.CurrentWeightKg;
        goal.TargetWeight = 63.7m;
        var savedPlan = goal.CaloriePlanJson;
        profile.CurrentWeightKg = 64m;
        var budget = Budgets().Calculate(profile, goal);

        Assert.Equal(25m, calculator.CalculateBmi(profile.CurrentWeightKg, profile.HeightCm));
        Assert.True(planner.Analyze(profile, GoalType.Gain, 65m, 4).IsBlocked);
        Assert.False(planner.CanApply(profile, GoalType.Gain, 63.7m, 100m, out _));
        Assert.True(budget.Suspended);
        Assert.Equal("Tạm ngừng điều chỉnh năng lượng", budget.Phase);
        Assert.Equal(0m, budget.Adjustment);
        Assert.Equal(savedPlan, goal.CaloriePlanJson);
    }
    [Theory]
    [InlineData(GoalType.Lose, 641)]
    [InlineData(GoalType.Gain, 659)]
    public void WeightChangeBelowOneKilogram_PromptsForAnotherTarget(GoalType type, int targetInTenths)
    {
        var result = new GoalPlanningService(calculator).Analyze(Profile(65m), type, targetInTenths / 10m, 16);

        Assert.True(result.IsBlocked);
        Assert.Equal(GoalPolicy.GoalTargetPrompt, result.BlockedReason);
    }
    [Fact]
    public void WeightChange_RequiresTwoWeeksButMaintenanceAllowsOne()
    {
        var planner = new GoalPlanningService(calculator);
        var shortPlan = planner.Analyze(Profile(), GoalType.Lose, 71m, 1);
        var minimumPlan = planner.Analyze(Profile(), GoalType.Lose, 71m, 2);
        var maintenance = planner.Analyze(Profile(), GoalType.Maintain, 72m, 1);

        Assert.True(shortPlan.IsBlocked);
        Assert.Contains("2 tuần", shortPlan.BlockedReason);
        Assert.False(minimumPlan.IsBlocked);
        Assert.False(maintenance.IsBlocked);
    }
    [Fact] public void Under18_HasNoAdultPlanOrBmiClassification()
    {
        var p = Profile(); p.DateOfBirth = GoalPolicy.Today.AddYears(-17);
        Assert.True(new GoalPlanningService(calculator).Analyze(p, GoalType.Lose, 65, 16).IsBlocked);
        Assert.Contains("theo tuổi", calculator.GetBmiInfo(72, 170, 17).Category);
    }
    [Theory]
    [InlineData(null, ScreeningStatus.NotScreened)]
    [InlineData(ScreeningAnswer.No, ScreeningStatus.NoReportedFactors)]
    [InlineData(ScreeningAnswer.Yes, ScreeningStatus.NeedsReview)]
    [InlineData(ScreeningAnswer.Unsure, ScreeningStatus.NeedsReview)]
    public void LegacyNotesRequireNewScreeningRegardlessOfTheOlderAnswers(ScreeningAnswer? answer, ScreeningStatus expected)
    {
        var p = Profile();
        p.HealthNotes = "Ghi chú cũ chưa có cấu trúc";
        p.HealthScreeningJson = answer.HasValue
            ? HealthScreening.Update(null, QaApplication.NoReportedFactors with { ConditionOrTreatment = answer })
            : null;
        Assert.Equal(expected, HealthScreening.StatusOf(p.HealthScreeningJson));
        var plan = new GoalPlanningService(calculator).Analyze(p, GoalType.Lose, 65, 16);
        Assert.True(plan.IsBlocked);
        Assert.Empty(plan.PaceOptions);
        Assert.False(plan.RequiresIntensePaceAck);
    }

    [Fact]
    public void HealthScreening_MalformedUnknownOrPartialDataCannotAllowAPlan()
    {
        Assert.Equal(ScreeningStatus.NotScreened, HealthScreening.StatusOf("{broken"));
        Assert.Equal(ScreeningStatus.NotScreened, HealthScreening.StatusOf("{\"Version\":99}"));
        var partial = new HealthScreeningAnswers { ConditionOrTreatment = ScreeningAnswer.No };
        Assert.Equal(ScreeningStatus.NotScreened, partial.Evaluate());
        var invalid = QaApplication.NoReportedFactors with { ConditionOrTreatment = (ScreeningAnswer)100 };
        Assert.Equal(ScreeningStatus.NeedsReview, invalid.Evaluate());
    }
    [Fact] public void InvalidDirectionAndTargetBmiRejected()
    {
        var planner = new GoalPlanningService(calculator);
        Assert.True(planner.Analyze(Profile(), GoalType.Lose, 75, 16).IsBlocked);
        Assert.True(planner.Analyze(Profile(), GoalType.Lose, 40, 104).IsBlocked);
        Assert.True(planner.Analyze(Profile(), GoalType.Gain, 65, 16).IsBlocked);
        Assert.True(planner.Analyze(Profile(), (GoalType)999, 65, 16).IsBlocked);
    }
    [Fact] public void IntroductoryPlan_DoesNotAdvanceByTime()
    {
        var r = new GoalPlanningService(calculator).Analyze(Profile(), GoalType.Lose, 65, 16, true);
        Assert.Equal(250m, r.PlannedDailyAdjustment); Assert.Equal(31, r.EstimatedWeeks);
        var goal = Goal(250); var state = CaloriePlanState.Read(goal.CaloriePlanJson)!; state.Introductory = true;
        goal.CaloriePlanJson = state.ToJson();
        Assert.Equal(250m, Budgets().Calculate(Profile(69), goal).Adjustment);
    }
    [Fact] public void SparseWeights_DoNotImplyPlateau()
    {
        var goal = Goal(); var budget = Budgets().Calculate(Profile(), goal);
        var review = AdaptiveCalorieService.Evaluate(budget, Budgets().StateFor(goal),
            new[] { new WeightLog { LoggedOn = GoalPolicy.Today, WeightKg = 72 } }, Array.Empty<MealDay>(), GoalPolicy.Today);
        Assert.False(review.SufficientData); Assert.Null(review.ProposedAdjustment); Assert.Contains("Không kết luận", review.Message);
    }
    private List<WeightLog> Logs(decimal lossPerDay, int days = 28, decimal startWeight = 72m) =>
        Enumerable.Range(0, days).Select(i => new WeightLog {
            LoggedOn = GoalPolicy.Today.AddDays(i - days + 1), WeightKg = startWeight - i * lossPerDay }).ToList();
    private List<MealDay> Meals(int calories = 2085) =>
        Enumerable.Range(0, 28).Select(i => new MealDay(GoalPolicy.Today.AddDays(i - 27), calories, 0)).ToList();
    [Fact] public void ProlongedSlowTrend_WithMealCoverage_ProposesButNeverApplies()
    {
        var goal = Goal(); var budget = Budgets().Calculate(Profile(), goal);
        var review = AdaptiveCalorieService.Evaluate(budget, Budgets().StateFor(goal), Logs(.01m), Meals(), GoalPolicy.Today);
        Assert.Equal(600m, review.ProposedAdjustment); Assert.True(review.MayIncrease);
        Assert.Equal(500m, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
    }
    [Fact] public void SlowTrend_WithoutFoodRecords_DoesNotIncrease()
    {
        var goal = Goal(); var review = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal), Budgets().StateFor(goal), Logs(.01m), Array.Empty<MealDay>(), GoalPolicy.Today);
        Assert.Null(review.ProposedAdjustment);
    }
    [Fact] public void FastLoss_ProposesLessDeficit()
    {
        var goal = Goal(); var review = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal),
            Budgets().StateFor(goal), Logs(.12m), Meals(), GoalPolicy.Today);
        Assert.Equal(.84m, review.WeeklyRate);
        Assert.Equal(350m, review.ProposedAdjustment); Assert.False(review.MayIncrease);
    }
    [Fact]
    public void FastGain_ProposesLessSurplusBelowTheOldFixedThreshold()
    {
        var service = Budgets();
        var goal = Goal(400m);
        goal.GoalType = GoalType.Gain;
        goal.StartWeight = 60m;
        var review = AdaptiveCalorieService.Evaluate(service.Calculate(Profile(62m), goal),
            service.StateFor(goal), Logs(-.068m, startWeight: 60m), Meals(), GoalPolicy.Today);

        Assert.Equal(.476m, review.WeeklyRate);
        Assert.Equal(250m, review.ProposedAdjustment);
        Assert.False(review.MayIncrease);
    }
    [Fact]
    public void SlowTrend_CapsIncreaseAtTheCurrentProfileLimit()
    {
        var service = Budgets();
        var goal = Goal(600m);
        var budget = service.Calculate(Profile(69m), goal);
        var review = AdaptiveCalorieService.Evaluate(budget, service.StateFor(goal),
            Logs(.01m), Meals(budget.TargetCalories), GoalPolicy.Today);

        Assert.Equal(634.53125m, budget.MaximumAdjustment);
        Assert.Equal(634.53125m, review.ProposedAdjustment);
        Assert.True(review.MayIncrease);
    }
    [Fact]
    public void IntroductoryReview_CapsPromotionAtTheCalorieFloor()
    {
        var service = Budgets();
        var profile = Profile(60m);
        profile.ActivityLevel = ActivityLevel.Sedentary;
        var goal = Goal(250m);
        goal.StartWeight = 60m;
        goal.TargetWeight = 56m;
        var state = service.StateFor(goal);
        state.Introductory = true;
        goal.CaloriePlanJson = state.ToJson();
        var budget = service.Calculate(profile, goal);
        var review = AdaptiveCalorieService.Evaluate(budget, state,
            Logs(.01m, startWeight: 60m), Meals(budget.TargetCalories), GoalPolicy.Today);

        Assert.Equal(357m, budget.MaximumAdjustment);
        Assert.Equal(357m, review.ProposedAdjustment);
    }
    [Fact] public void OnPlanTrend_KeepsAdjustment()
    {
        var goal = Goal(); var review = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal), Budgets().StateFor(goal), Logs(.065m), Meals(), GoalPolicy.Today);
        Assert.Null(review.ProposedAdjustment);
    }
    [Fact] public void Maintain_DoesNotReactToOneDay()
    {
        var goal = Goal(0); goal.GoalType = GoalType.Maintain;
        var review = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal), Budgets().StateFor(goal), Logs(.01m), Meals(), GoalPolicy.Today);
        Assert.Null(review.ProposedAdjustment); Assert.Contains("duy trì", review.Message);
    }
    [Fact] public void DuplicateDailyWeights_AreNotIndependentData()
    {
        var goal = Goal(); var logs = Enumerable.Range(0, 20).Select(i => new WeightLog { LoggedOn = GoalPolicy.Today, WeightKg = 72 });
        var review = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal), Budgets().StateFor(goal), logs, Meals(), GoalPolicy.Today);
        Assert.False(review.SufficientData); Assert.Equal(1, review.WeightDays);
    }
    [Fact] public void StagedForecast_SumsEachStageEnergy()
    {
        Assert.Equal(115, GoalPolicy.EstimatePlannedDays(7, new[] { new EnergyStage(14, 250), new EnergyStage(null, 500) }));
        var result = new GoalPlanningService(calculator).Analyze(Profile(), GoalType.Lose, 65, 16, true);
        Assert.Equal(16, result.StagedEstimateWeeks);
        Assert.Equal(250, result.PlannedDailyAdjustment); // Future stage still requires review and consent.
    }
    [Fact] public void SlowTrend_IsStillConsideredWhenPreviousReviewKeptPlan()
    {
        var goal = Goal(); var state = Budgets().StateFor(goal);
        state.LastReviewedOn = GoalPolicy.Today.AddDays(-14);
        var result = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal), state, Logs(.01m), Meals(), GoalPolicy.Today);
        Assert.Equal(600m, result.ProposedAdjustment);
    }
    [Fact] public void NewlyChangedPlan_DoesNotReuseOldSlowTrend()
    {
        var goal = Goal(); var state = Budgets().StateFor(goal);
        state.Changes.Add(new(GoalPolicy.Today.AddDays(-14), 400, "Đã đổi kế hoạch"));
        var result = AdaptiveCalorieService.Evaluate(Budgets().Calculate(Profile(), goal), state, Logs(.01m), Meals(), GoalPolicy.Today);
        Assert.Null(result.ProposedAdjustment);
    }
    [Fact] public void UnsafeLegacyPlan_IsSuspended()
    {
        var result = Budgets().Calculate(Profile(), Goal(900));
        Assert.True(result.Suspended); Assert.Equal(0m, result.Adjustment); Assert.Equal(2585, result.TargetCalories);
    }
    [Fact]
    public void ConfirmedPlanAboveNewLimit_IsSuspendedWithoutReplacingItsAdjustment()
    {
        var goal = Goal(700m);
        var savedPlan = goal.CaloriePlanJson;
        var budget = Budgets().Calculate(Profile(69m), goal);

        Assert.True(budget.Suspended);
        Assert.Equal(0m, budget.Adjustment);
        Assert.Equal(2538, budget.TargetCalories);
        Assert.Equal(savedPlan, goal.CaloriePlanJson);
        Assert.Equal(700m, CaloriePlanState.Read(goal.CaloriePlanJson)!.Adjustment);
    }
    [Fact] public void GoalReached_ProposesMaintenance()
    {
        var service = Budgets();
        var goal = Goal();
        goal.TargetWeight = 71.9m;
        var budget = service.Calculate(Profile(), goal);
        Assert.False(budget.Suspended);
        var result = AdaptiveCalorieService.Evaluate(budget, service.StateFor(goal), Logs(.01m), Meals(), GoalPolicy.Today);
        Assert.True(result.GoalReached); Assert.Equal(0m, result.ProposedAdjustment);
    }
}
