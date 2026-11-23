using System.Security.Cryptography;
using System.Text.Json;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

// These limits are project policy, not a medical protocol.
public record EnergyStage(int? Days, decimal DailyAdjustment);

public enum BodyFeedback { Keep, Increase, Decrease }
public enum PaceZone { None, VerySlow, Gentle, Standard, Intense, OverLimit }

public sealed record PaceOption(string Key, string Label, int Weeks, DateTime? TargetDate, decimal WeeklyRateKg,
    decimal DailyAdjustmentKcal, int? DailyBudgetKcal, PaceZone PaceZone, bool RequiresIntensePaceAck,
    bool IsRecommended, bool IsAvailable, string? UnavailableReason)
{
    public string PaceZoneLabel => CalorieTargetResult.LabelFor(PaceZone);
}

public record BodyAdjustmentPreview(int GoalId, BodyFeedback Feedback, int OldCalories, int NewCalories,
    decimal OldWeeklyRate, decimal NewWeeklyRate, int? NewWeeks, DateTime? NewTargetDate,
    decimal Adjustment, string Fingerprint);

public static partial class GoalPolicy
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();
    private static readonly AsyncLocal<DateTime?> UtcNowOverride = new();

    public const decimal EnergyPerKg = 7700m;
    public const decimal MaxDeficitNormalBmi = 500m;
    public const decimal MaxDeficitModerateBmi = 750m;
    public const decimal MaxDeficitHighBmi = 1000m;
    public const decimal MaximumSurplus = 500m;
    public const decimal DeficitNormalBmiTdeeRate = 0.20m;
    public const decimal DeficitModerateBmiTdeeRate = 0.25m;
    public const decimal DeficitHighBmiTdeeRate = 0.35m;
    public const decimal ActivePlanCapTolerance = 1.10m;
    public const decimal SurplusTdeeRate = 0.20m;
    public const decimal WeeklyLossWeightRate = 0.01m;
    public const decimal WeeklyGainWeightRate = 0.0075m;
    public const decimal MaximumWeeklyLossKg = 1m;
    public const decimal MaximumWeeklyGainKg = 0.5m;
    public const decimal StartingLossRateKg = 0.5m;
    public const decimal GentleLossRateKg = 0.25m;
    public const decimal IntensePaceWeeklyKg = 0.75m;
    public const decimal GentleGainRateKg = 0.2m;
    public const decimal StandardGainRateKg = 0.3m;
    public const decimal QuickGainRateKg = 0.4m;
    public const decimal VerySlowGainRateKg = 0.1m;
    public const decimal UsualDeficit = StartingLossRateKg * EnergyPerKg / 7m;
    public const decimal BodyAdjustmentStep = 175m;
    public const int BodyCheckInDays = 7;
    public const decimal IntroAdjustment = 250m;
    public const int ReviewDays = 14;
    public const decimal MinGoalChangeKg = 1m;
    public const string GoalTargetPrompt = "Hãy nhập cân nặng mong muốn khác cân hiện tại (giảm: thấp hơn, tăng: cao hơn)";
    public const decimal LargeWeightChangeKg = 2m;
    public const decimal LargeWeightChangePercent = 0.03m;
    public const int LargeWeightChangeWindowDays = 14;
    public const int MinGoalWeeks = ReviewDays / 7;
    public const int MaxGoalWeeks = 104;
    public const string IntensePaceAcknowledgmentMessage = "Hãy xác nhận rằng bạn đã xem tốc độ dự kiến và mức calo trước khi tiếp tục.";
    public const decimal MinimumAdultBmi = 18.5m;
    public const decimal BmiModerate = 23m;
    public const decimal BmiHigh = 25m;
    public const decimal OverweightBmi = BmiModerate;
    public const decimal ObesityBmi = BmiHigh;
    public const string AdultBmiReference = "Mốc BMI người trưởng thành theo Bảng 4.1, Quyết định 2892/QĐ-BYT (18,5 / 23 / 25).";
    public const decimal NormalWeightLimitBmi = 22.9m;
    public const decimal GainMaxCurrentBmi = ObesityBmi;
    public const decimal GainMaxTargetBmi = 24.9m;
    public const decimal ExerciseCreditRate = 0.5m;
    public const decimal MaxExerciseCredit = 300m;
    public static decimal CalorieFloor(Gender gender) => gender == Gender.Female ? 1200m : 1500m;
    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(UtcNowOverride.Value ?? DateTime.UtcNow, VietnamTimeZone);
    public static DateTime Today => Now.Date;

    internal static DateTime VietnamDateStartUtc(DateTime date) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), VietnamTimeZone);

    internal static IDisposable OverrideUtcNow(DateTime utcNow)
    {
        var scope = new ClockOverride(UtcNowOverride.Value);
        UtcNowOverride.Value = utcNow;
        return scope;
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("SE Asia Standard Time", out var windowsZone))
            return windowsZone;
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Ho_Chi_Minh", out var ianaZone))
            return ianaZone;
        return TimeZoneInfo.CreateCustomTimeZone("Vietnam UTC+7", TimeSpan.FromHours(7), "Vietnam UTC+7", "Vietnam UTC+7");
    }

    private sealed class ClockOverride(DateTime? previous) : IDisposable
    {
        public void Dispose() => UtcNowOverride.Value = previous;
    }

    public static int RoundCalories(decimal value) => (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
    public static decimal ExerciseCredit(decimal burned) =>
        RoundCalories(Math.Clamp(burned * ExerciseCreditRate, 0m, MaxExerciseCredit));

    public static decimal RemainingCalories(int target, decimal consumed, decimal burned) =>
        target - consumed + ExerciseCredit(burned);

    public static decimal MaxDeficit(decimal tdee, decimal weightKg, decimal heightCm) =>
        DeficitCap(tdee, weightKg, heightCm, weightKg);

    private static decimal DeficitCap(decimal tdee, decimal weightKg, decimal heightCm, decimal bmiWeightKg)
    {
        if (tdee <= 0 || weightKg <= 0 || heightCm <= 0) return 0;
        var heightM = heightCm / 100m;
        var (absolute, fraction) = (bmiWeightKg / (heightM * heightM)) switch
        {
            < BmiModerate => (MaxDeficitNormalBmi, DeficitNormalBmiTdeeRate),
            < BmiHigh => (MaxDeficitModerateBmi, DeficitModerateBmiTdeeRate),
            _ => (MaxDeficitHighBmi, DeficitHighBmiTdeeRate)
        };
        return Math.Min(absolute, Math.Min(fraction * tdee, weightKg * WeeklyLossWeightRate * EnergyPerKg / 7m));
    }

    public static decimal MaxSurplus(decimal tdee) =>
        Math.Min(MaximumSurplus, SurplusTdeeRate * tdee);

    public static decimal MaximumAdjustment(GoalType type, decimal tdee, Gender gender, decimal weightKg,
        decimal heightCm, decimal? startWeightKg = null, bool allowTolerance = false, HealthAssessment? health = null)
    {
        var baseline = type switch
        {
            GoalType.Lose => Math.Min(
                DeficitCap(tdee, weightKg, heightCm, Math.Max(weightKg, startWeightKg ?? weightKg)) *
                    (startWeightKg.HasValue && allowTolerance ? ActivePlanCapTolerance : 1m),
                Math.Max(0m, tdee - CalorieFloor(gender))),
            GoalType.Gain => MaxSurplus(tdee),
            _ => 0m
        };
        if (health?.SuspensionReason != null) return 0m;
        return health?.Tier == HealthTier.NeedsDoctor ? Math.Min(baseline, DoctorAwareAdjustmentCap) : baseline;
    }

    public static decimal MaximumWeeklyRate(GoalType type, decimal weightKg) => type == GoalType.Lose
        ? Math.Min(MaximumWeeklyLossKg, WeeklyLossWeightRate * weightKg)
        : Math.Min(MaximumWeeklyGainKg, WeeklyGainWeightRate * weightKg);

    public static (decimal Minimum, decimal NormalMaximum, decimal GainMaximum) BmiWeightLimits(decimal heightCm)
    {
        var heightM = heightCm / 100m;
        var heightSquared = heightM * heightM;
        return (
            Math.Ceiling(MinimumAdultBmi * heightSquared * 10m) / 10m,
            Math.Floor(NormalWeightLimitBmi * heightSquared * 10m) / 10m,
            Math.Floor(GainMaxTargetBmi * heightSquared * 10m) / 10m);
    }

    public static int EstimatePlannedDays(decimal kg, IEnumerable<EnergyStage> stages)
    {
        var remaining = EstimateEnergy(kg);
        var days = 0;
        foreach (var stage in stages)
        {
            if (stage.DailyAdjustment <= 0 || stage.Days is <= 0) throw new ArgumentOutOfRangeException(nameof(stages));
            var needed = (int)Math.Ceiling(remaining / stage.DailyAdjustment);
            if (stage.Days == null || needed <= stage.Days.Value) return days + needed;
            remaining -= stage.Days.Value * stage.DailyAdjustment;
            days += stage.Days.Value;
        }
        if (remaining > 0) throw new ArgumentException("Kế hoạch chưa đủ năng lượng để ước tính ngày đạt đích.", nameof(stages));
        return days;
    }
    public static decimal EstimateEnergy(decimal kg) => Math.Abs(kg) * EnergyPerKg;

    public static decimal WeeklyRate(decimal dailyAdjustment) => dailyAdjustment * 7m / EnergyPerKg;

    public static DateTime? EstimateTargetDate(decimal remainingKg, decimal adjustment, DateTime today)
    {
        if (adjustment <= 0m) return null;
        var days = Math.Ceiling(EstimateEnergy(remainingKg) / adjustment);
        return days <= (DateTime.MaxValue.Date - today.Date).Days ? today.Date.AddDays((double)days) : null;
    }
}

public class GoalPlanningService(INutritionCalculatorService calculator)
{
    public static bool IsBodyCheckInDue(Goal goal, CaloriePlanState state, DateTime today)
    {
        var lastCheckIn = state.LastBodyCheckInOn?.Date ?? goal.StartDate.Date;
        var lastChange = state.Changes.LastOrDefault()?.On.Date ?? goal.StartDate.Date;
        var anchor = lastCheckIn > lastChange ? lastCheckIn : lastChange;
        return goal.IsActive && goal.GoalType == GoalType.Lose
            && (today.Date - anchor).Days >= GoalPolicy.BodyCheckInDays;
    }

    public BodyAdjustmentPreview? PreviewBodyAdjustment(UserProfile profile, Goal goal, CalorieBudget budget,
        BodyFeedback feedback, DateTime today, out string? reason)
    {
        reason = null;
        if (goal.GoalType != GoalType.Lose || !goal.IsActive || feedback is not (BodyFeedback.Increase or BodyFeedback.Decrease))
        {
            reason = "Chỉ có thể điều chỉnh tốc độ của mục tiêu giảm cân đang hoạt động.";
            return null;
        }
        if (budget.Suspended || profile.CurrentWeightKg <= goal.TargetWeight)
        {
            reason = "Kế hoạch đang tạm ngừng hoặc cân nặng đã đến đích. Hãy xem lại mục tiêu trước khi đổi tốc độ.";
            return null;
        }

        var adjustment = feedback == BodyFeedback.Increase
            ? Math.Min(budget.MaximumAdjustment, budget.Adjustment + GoalPolicy.BodyAdjustmentStep)
            : Math.Max(0m, budget.Adjustment - GoalPolicy.BodyAdjustmentStep);
        if (adjustment == budget.Adjustment || (feedback == BodyFeedback.Increase && adjustment < budget.Adjustment))
        {
            reason = feedback == BodyFeedback.Increase
                ? $"Hồ sơ đã chạm trần thâm hụt ({budget.MaximumAdjustment:0.##} kcal/ngày), sau khi tính cả sàn calo."
                : "Mức thâm hụt đang là 0 kcal/ngày.";
            return null;
        }
        if (!CanApply(profile, GoalType.Lose, goal.TargetWeight, adjustment, out reason, goal.StartWeight, allowTolerance: false))
            return null;

        today = today.Date;
        var targetDate = GoalPolicy.EstimateTargetDate(profile.CurrentWeightKg - goal.TargetWeight, adjustment, today);
        int? weeks = targetDate.HasValue ? (int)Math.Ceiling((targetDate.Value - today).Days / 7m) : null;
        var snapshot = JsonSerializer.SerializeToUtf8Bytes(new
        {
            goal.Id, goal.CaloriePlanJson, goal.TargetDate, goal.TargetWeight, CurrentAdjustment = budget.Adjustment,
            budget.Tdee, budget.MaximumAdjustment, profile.CurrentWeightKg, today, feedback, ProposedAdjustment = adjustment
        });
        return new(goal.Id, feedback, budget.TargetCalories, GoalPolicy.RoundCalories(budget.Tdee - adjustment),
            GoalPolicy.WeeklyRate(budget.Adjustment), GoalPolicy.WeeklyRate(adjustment), weeks, targetDate,
            adjustment, Convert.ToHexString(SHA256.HashData(snapshot)));
    }

    public CalorieTargetResult Analyze(UserProfile profile, GoalType type, decimal? target, int weeks, bool introductory = false)
    {
        var age = calculator.CalculateAge(profile.DateOfBirth);
        var bmr = calculator.CalculateBmr(profile.Gender, profile.CurrentWeightKg, profile.HeightCm, age);
        var health = GoalPolicy.EvaluateHealth(profile, age);
        var result = AnalyzeEnergy(calculator.CalculateTdee(bmr, profile.ActivityLevel), type, profile.Gender,
            profile.CurrentWeightKg, target, weeks, profile.HeightCm, introductory, health);
        result.Bmr = bmr;
        var restriction = ScopeRestriction(profile, age, health);
        if (restriction != null)
        {
            Block(result, restriction, age < 13 || age > 100);
            result.RequiresHealthScreening = health.SuspensionReason != null;
            result.HideEnergyDetails = true;
            result.PaceOptions = [];
            result.PaceZone = PaceZone.None;
            result.MaxWeeklyRateKg = 0;
            result.PlannedDailyAdjustment = 0;
            result.DailyCalorieTarget = 0;
            result.EstimatedWeeks = 0;
            result.MinimumMathematicalWeeks = result.RecommendedWeeks = result.MinRecommendedWeeks = result.MaxRecommendedWeeks = 0;
        }
        return result;
    }

    public static string? ScopeRestriction(UserProfile profile, int age, HealthAssessment? health = null)
    {
        health ??= GoalPolicy.EvaluateHealth(profile, age);
        if (age > 100 || profile.HeightCm is < 100 or > 250 || profile.CurrentWeightKg is < 20 or > 300 ||
            !Enum.IsDefined(profile.Gender) || !Enum.IsDefined(profile.ActivityLevel))
            return "Hồ sơ chưa đủ dữ liệu hợp lệ để đánh giá.";
        return health.SuspensionReason;
    }

    public static CalorieTargetResult AnalyzeEnergy(decimal tdee, GoalType type, Gender gender,
        decimal current, decimal? target, int weeks, decimal height, bool introductory = false, HealthAssessment? health = null) =>
        AnalyzeEnergy(tdee, type, gender, current, target, weeks, height, introductory, health, includeOptions: true);

    private static CalorieTargetResult AnalyzeEnergy(decimal tdee, GoalType type, Gender gender,
        decimal current, decimal? target, int weeks, decimal height, bool introductory, HealthAssessment? health, bool includeOptions)
    {
        if (type == GoalType.Maintain) target = current;
        var heightM = height / 100m;
        var limits = GoalPolicy.BmiWeightLimits(height);
        var result = new CalorieTargetResult { Tdee = tdee, DurationWeeks = weeks,
            DailyCalorieTarget = GoalPolicy.RoundCalories(tdee),
            CurrentBmi = heightM > 0 ? current / (heightM * heightM) : 0,
            TargetBmi = heightM > 0 ? target.GetValueOrDefault() / (heightM * heightM) : 0,
            MinSafeWeight = limits.Minimum,
            MaxNormalWeight = limits.NormalMaximum,
            MaxAllowedWeight = limits.GainMaximum, Health = health };
        if (!Enum.IsDefined(type) || !Enum.IsDefined(gender) ||
            current is < 20 or > 300 || target is < 20 or > 300 || height is < 100 or > 250 || tdee <= 0)
        {
            Block(result, "Vui lòng nhập hồ sơ hợp lệ và cân nặng từ 20 đến 300 kg.", true);
            result.HideEnergyDetails = true;
            return result;
        }
        if (health?.SuspensionReason is { } restriction)
        {
            Block(result, restriction);
            result.RequiresHealthScreening = true;
            result.HideEnergyDetails = true;
            result.DailyCalorieTarget = 0;
            return result;
        }
        if ((type == GoalType.Lose && (!target.HasValue || current - target < GoalPolicy.MinGoalChangeKg)) ||
            (type == GoalType.Gain && (!target.HasValue || target - current < GoalPolicy.MinGoalChangeKg)))
        {
            Block(result, GoalPolicy.GoalTargetPrompt);
            result.HideEnergyDetails = true;
            return result;
        }

        var delta = type == GoalType.Maintain ? 0 : Math.Abs(current - target.GetValueOrDefault());
        result.WeightDifferenceKg = delta;
        var max = GoalPolicy.MaximumAdjustment(type, tdee, gender, current, height, health: health);
        var usual = Math.Min(type == GoalType.Lose ? GoalPolicy.UsualDeficit : GoalPolicy.MaximumSurplus, max);
        result.MinimumMathematicalWeeks = EstimateWeeks(delta, max);
        result.RecommendedWeeks = delta == 0 ? 4 : EstimateWeeks(delta, usual);
        result.MinRecommendedWeeks = result.MinimumMathematicalWeeks;
        result.MaxRecommendedWeeks = Math.Max(result.RecommendedWeeks, result.RecommendedWeeks * 2);

        if (type == GoalType.Lose && (result.CurrentBmi < GoalPolicy.MinimumAdultBmi || result.TargetBmi < GoalPolicy.MinimumAdultBmi))
            Block(result, $"Mục tiêu giảm cân phải thấp hơn cân nặng hiện tại và không đưa BMI xuống dưới {GoalPolicy.MinimumAdultBmi:0.#}. BMI chỉ là chỉ số sàng lọc.");
        else if (type == GoalType.Gain && (result.CurrentBmi >= GoalPolicy.GainMaxCurrentBmi || result.TargetBmi > GoalPolicy.GainMaxTargetBmi))
            Block(result, "Mục tiêu tăng cân chưa phù hợp giới hạn BMI của đồ án. Hãy điều chỉnh cân nặng đích hoặc tham khảo chuyên gia.");
        else if (type == GoalType.Lose && max <= 0)
            Block(result, "TDEE không cho phép tạo thâm hụt trong sàn calo đã cấu hình.");
        if (result.IsBlocked)
        {
            result.HideEnergyDetails = true;
            return result;
        }

        if (type != GoalType.Maintain)
        {
            result.MaxWeeklyRateKg = Math.Round(GoalPolicy.WeeklyRate(max), 2, MidpointRounding.AwayFromZero);
            if (includeOptions)
                result.PaceOptions = CreatePaceOptions(tdee, type, gender, current, target!.Value, height,
                    health, result.MinimumMathematicalWeeks);
        }

        if (weeks > 0)
        {
            result.WeeklyRateKg = delta / weeks;
            result.RequiredDailyAdjustment = GoalPolicy.EstimateEnergy(delta) / (weeks * 7m);
        }
        if (weeks is < 1 or > GoalPolicy.MaxGoalWeeks)
        {
            Block(result, $"Thời gian thực hiện phải từ 1 đến {GoalPolicy.MaxGoalWeeks} tuần.", true);
            return result;
        }

        if (type != GoalType.Maintain && weeks < GoalPolicy.MinGoalWeeks)
            Block(result, $"Mục tiêu giảm hoặc tăng cân cần ít nhất {GoalPolicy.MinGoalWeeks} tuần.");
        else if (result.RequiredDailyAdjustment > max)
        {
            result.PaceZone = PaceZone.OverLimit;
            Block(result, $"Thời gian {weeks} tuần cần mức điều chỉnh ước tính {result.RequiredDailyAdjustment:0.##} kcal/ngày, vượt giới hạn cho hồ sơ ({max:0.##} kcal/ngày). Hãy áp dụng thời gian đề xuất hoặc kéo dài thêm.");
        }
        if (result.IsBlocked) return result;

        result.PaceZone = GetPaceZone(type, result.WeeklyRateKg);
        result.PlannedDailyAdjustment = introductory
            ? Math.Min(result.RequiredDailyAdjustment, GoalPolicy.IntroAdjustment)
            : result.RequiredDailyAdjustment;
        result.EstimatedWeeks = delta == 0 || result.PlannedDailyAdjustment == result.RequiredDailyAdjustment
            ? weeks : EstimateWeeks(delta, result.PlannedDailyAdjustment);
        if (introductory && result.PlannedDailyAdjustment > 0 && usual > result.PlannedDailyAdjustment)
        {
            result.StagedEstimateWeeks = (int)Math.Ceiling(GoalPolicy.EstimatePlannedDays(delta, new[] {
                new EnergyStage(GoalPolicy.ReviewDays, result.PlannedDailyAdjustment), new EnergyStage(null, usual) }) / 7m);
        }
        var sign = type == GoalType.Lose ? -1 : 1;
        result.DeficitOrSurplusCalories = GoalPolicy.RoundCalories(result.PlannedDailyAdjustment) * sign;
        result.DailyCalorieTarget = GoalPolicy.RoundCalories(tdee + sign * result.PlannedDailyAdjustment);
        if (introductory && type != GoalType.Maintain) result.IntroductoryDailyBudgetKcal = result.DailyCalorieTarget;

        var warning = result.PaceZone switch
        {
            PaceZone.VerySlow when type == GoalType.Lose =>
                $"Tốc độ {result.WeeklyRateKg:0.##} kg/tuần rất chậm, khó thấy thay đổi qua cân vì cân dao động theo nước." +
                (result.RecommendedWeeks < weeks
                    ? $" Có thể rút xuống khoảng {result.RecommendedWeeks} tuần (≈ {GoalPolicy.WeeklyRate(usual):0.##} kg/tuần)." : ""),
            PaceZone.VerySlow => "Tốc độ thấp, thay đổi có thể khó nhận thấy qua từng lần cân. Hãy theo dõi xu hướng qua nhiều ngày.",
            PaceZone.Intense => "Tốc độ mong muốn thuộc vùng Mạnh. Hãy xem mức calo dự kiến và theo dõi tiến độ định kỳ.",
            _ => null
        };
        if (type != GoalType.Maintain && (result.WeeklyRateKg > GoalPolicy.MaximumWeeklyRate(type, current) ||
            result.TargetBmi < 19.5m || result.TargetBmi >= GoalPolicy.ObesityBmi))
            warning = $"{warning} Hãy cân nhắc tốc độ hoặc BMI đích.".Trim();
        if (warning != null)
        {
            result.SafetyLevel = GoalSafetyLevel.MildWarning;
            result.WarningFlag = true;
            result.WarningMessage = warning;
        }
        return result;
    }

    private static int EstimateWeeks(decimal delta, decimal dailyAdjustment)
    {
        if (delta <= 0 || dailyAdjustment <= 0) return 0;
        var energy = GoalPolicy.EstimateEnergy(delta);
        // Preserve a clear out-of-range result when the calculation cannot fit in an int.
        if (dailyAdjustment <= energy / (7m * (int.MaxValue / 2))) return int.MaxValue / 2;
        return Math.Max(GoalPolicy.MinGoalWeeks, (int)Math.Ceiling(energy / (dailyAdjustment * 7m)));
    }

    private static PaceOption[] CreatePaceOptions(decimal tdee, GoalType type, Gender gender,
        decimal current, decimal target, decimal height, HealthAssessment? health, int fastestWeeks)
    {
        var delta = Math.Abs(current - target);
        var (gentle, standard, quick) = type == GoalType.Lose
            ? (GoalPolicy.GentleLossRateKg, GoalPolicy.StartingLossRateKg, GoalPolicy.IntensePaceWeeklyKg)
            : (GoalPolicy.GentleGainRateKg, GoalPolicy.StandardGainRateKg, GoalPolicy.QuickGainRateKg);
        var today = GoalPolicy.Today;
        int WeeksAt(decimal rate) => Math.Max(GoalPolicy.MinGoalWeeks, (int)Math.Ceiling(delta / rate));

        PaceOption Option(string key, string label, int weeks, bool recommended = false)
        {
            var evaluated = AnalyzeEnergy(tdee, type, gender, current, target, weeks, height, false, health, includeOptions: false);
            var available = !evaluated.IsBlocked;
            var reason = available ? null : weeks > GoalPolicy.MaxGoalWeeks
                ? $"Vượt thời gian hỗ trợ tối đa {GoalPolicy.MaxGoalWeeks} tuần, hãy chia thành nhiều giai đoạn"
                : "Vượt giới hạn an toàn cho hồ sơ của bạn";
            var days = weeks * 7L;
            DateTime? date = days <= (DateTime.MaxValue.Date - today).Days ? today.AddDays(days) : null;
            return new(key, label, weeks, date, evaluated.WeeklyRateKg, evaluated.RequiredDailyAdjustment,
                available ? evaluated.DailyCalorieTarget : null, evaluated.PaceZone, evaluated.RequiresIntensePaceAck,
                recommended, available, reason);
        }

        PaceOption[] options =
        [
            Option("gentle", "Nhẹ nhàng", WeeksAt(gentle)),
            Option("standard", "Tiêu chuẩn", WeeksAt(standard), recommended: true),
            Option("quick", "Nhanh", WeeksAt(quick)),
            Option("fastest", "Nhanh nhất cho phép", fastestWeeks)
        ];
        return options.GroupBy(option => option.Weeks).Select(group =>
        {
            var option = group.FirstOrDefault(candidate => candidate.Key == "fastest") ??
                group.FirstOrDefault(candidate => candidate.IsRecommended) ?? group.First();
            return option with { IsRecommended = group.Any(candidate => candidate.IsRecommended) };
        }).ToArray();
    }

    private static PaceZone GetPaceZone(GoalType type, decimal weeklyRate) => type switch
    {
        GoalType.Lose when weeklyRate < GoalPolicy.GentleLossRateKg => PaceZone.VerySlow,
        GoalType.Lose when weeklyRate < GoalPolicy.StartingLossRateKg => PaceZone.Gentle,
        GoalType.Lose when weeklyRate > GoalPolicy.IntensePaceWeeklyKg => PaceZone.Intense,
        GoalType.Gain when weeklyRate < GoalPolicy.VerySlowGainRateKg => PaceZone.VerySlow,
        GoalType.Lose or GoalType.Gain => PaceZone.Standard,
        _ => PaceZone.None
    };

    public static void Block(CalorieTargetResult result, string reason, bool insufficient = false)
    {
        result.IsBlocked = result.WarningFlag = true;
        result.SafetyLevel = insufficient ? GoalSafetyLevel.InsufficientData : GoalSafetyLevel.Dangerous;
        result.BlockedReason = result.WarningMessage = reason;
    }

    public bool CanApply(UserProfile profile, GoalType type, decimal targetWeight, decimal adjustment, out string? reason,
        decimal? startWeightKg = null, bool allowTolerance = true)
    {
        var age = calculator.CalculateAge(profile.DateOfBirth);
        var health = GoalPolicy.EvaluateHealth(profile, age);
        reason = ScopeRestriction(profile, age, health);
        if (reason != null) return false;
        var bmi = calculator.CalculateBmi(profile.CurrentWeightKg, profile.HeightCm);
        var targetBmi = calculator.CalculateBmi(targetWeight, profile.HeightCm);
        var tdee = calculator.CalculateTdee(calculator.CalculateBmr(profile.Gender, profile.CurrentWeightKg,
            profile.HeightCm, age), profile.ActivityLevel);
        if (type == GoalType.Gain && (bmi >= GoalPolicy.GainMaxCurrentBmi || targetBmi > GoalPolicy.GainMaxTargetBmi))
        {
            reason = $"Kế hoạch tăng cân yêu cầu BMI hiện tại dưới {GoalPolicy.GainMaxCurrentBmi:0.#} và BMI đích tối đa {GoalPolicy.GainMaxTargetBmi:0.#}. Hãy xem lại mục tiêu.";
            return false;
        }
        if (!Enum.IsDefined(type) || adjustment < 0 ||
            adjustment > GoalPolicy.MaximumAdjustment(type, tdee, profile.Gender, profile.CurrentWeightKg,
                profile.HeightCm, startWeightKg, allowTolerance, health) ||
            (type == GoalType.Lose && (tdee - adjustment < GoalPolicy.CalorieFloor(profile.Gender) || bmi < GoalPolicy.MinimumAdultBmi || targetBmi < GoalPolicy.MinimumAdultBmi)))
        {
            reason = "Mức điều chỉnh không đáp ứng giới hạn năng lượng, sàn calo hoặc BMI của hệ thống. Hãy kéo dài thời gian hoặc xin hỗ trợ chuyên môn.";
            return false;
        }
        return true;
    }
}
