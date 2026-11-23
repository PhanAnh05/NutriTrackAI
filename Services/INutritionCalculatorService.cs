using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public class BmiResult
{
    public decimal Bmi { get; set; }
    public string Category { get; set; } = string.Empty;
    public string BadgeClass { get; set; } = string.Empty;
}

public class CalorieTargetResult
{
    public decimal Bmr { get; set; }
    public HealthAssessment? Health { get; set; }
    public bool RequiresHealthScreening { get; set; }
    public bool HideEnergyDetails { get; set; }
    public decimal Tdee { get; set; }
    public decimal WeightDifferenceKg { get; set; }
    public decimal RequiredDailyAdjustment { get; set; }
    public decimal PlannedDailyAdjustment { get; set; }
    public decimal AppliedWeeklyRateKg => GoalPolicy.WeeklyRate(PlannedDailyAdjustment);
    public decimal MaxWeeklyRateKg { get; set; }
    public PaceZone PaceZone { get; set; }
    public string PaceZoneLabel => LabelFor(PaceZone);
    public static string LabelFor(PaceZone zone) => zone switch
    {
        PaceZone.VerySlow => "Rất chậm",
        PaceZone.Gentle => "Nhẹ nhàng",
        PaceZone.Standard => "Tiêu chuẩn",
        PaceZone.Intense => "Mạnh",
        PaceZone.OverLimit => "Vượt giới hạn",
        _ => ""
    };
    public bool RequiresIntensePaceAck => !IsBlocked && PaceZone == PaceZone.Intense;
    public IReadOnlyList<PaceOption> PaceOptions { get; set; } = [];
    public int MinimumMathematicalWeeks { get; set; }
    public int EstimatedWeeks { get; set; }
    public int? StagedEstimateWeeks { get; set; }
    public int? IntroductoryDailyBudgetKcal { get; set; }
    public string StatusLabel => SafetyLevel switch
    {
        GoalSafetyLevel.Safe => "Phù hợp với quy tắc hệ thống",
        GoalSafetyLevel.MildWarning => "Cần cân nhắc",
        GoalSafetyLevel.InsufficientData => "Không đủ dữ liệu đánh giá",
        _ => "Cần điều chỉnh"
    };

    public int DailyCalorieTarget { get; set; }
    public decimal WeeklyRateKg { get; set; }
    public int DurationWeeks { get; set; }
    public bool WarningFlag { get; set; }
    public string? WarningMessage { get; set; }
    public int DeficitOrSurplusCalories { get; set; }

    // Kiểm soát 3 mức độ (Xanh / Vàng / Đỏ) & Khuyến nghị thời gian
    public GoalSafetyLevel SafetyLevel { get; set; } = GoalSafetyLevel.Safe;
    public bool IsBlocked { get; set; }
    public string? BlockedReason { get; set; }
    public int RecommendedWeeks { get; set; }
    public int MinRecommendedWeeks { get; set; }
    public int MaxRecommendedWeeks { get; set; }

    // Kiểm soát giới hạn Cân nặng mong muốn theo BMI đích
    public decimal CurrentBmi { get; set; }
    public decimal TargetBmi { get; set; }
    public decimal MinSafeWeight { get; set; }
    public decimal MaxNormalWeight { get; set; }
    public decimal MaxAllowedWeight { get; set; }
    public decimal SuggestedSafeWeight { get; set; }
}

public class MacroRecommendation
{
    public string? UnavailableReason { get; set; }
    public decimal ProteinGrams { get; set; }
    public decimal CarbGrams { get; set; }
    public decimal FatGrams { get; set; }
    public int ProteinPercent { get; set; }
    public int CarbPercent { get; set; }
    public int FatPercent { get; set; }
    public decimal ProteinCalories => ProteinGrams * 4;
    public decimal CarbCalories => CarbGrams * 4;
    public decimal FatCalories => FatGrams * 9;
}

public interface INutritionCalculatorService
{
    int CalculateAge(DateTime dob);
    decimal CalculateBmi(decimal weightKg, decimal heightCm);
    string GetBmiCategory(decimal bmi);
    BmiResult GetBmiInfo(decimal weightKg, decimal heightCm, int? age = null);
    decimal CalculateBmr(Gender gender, decimal weightKg, decimal heightCm, int age);
    decimal CalculateTdee(decimal bmr, ActivityLevel activityLevel);
    void GetBmiWeightLimits(decimal heightCm, out decimal minSafeWeight, out decimal maxNormalWeight, out decimal maxAllowedWeight);
    void GetRecommendedDuration(GoalType goalType, decimal currentWeight, decimal targetWeight, out int minWeeks, out int maxWeeks, out int recommendedWeeks);
    CalorieTargetResult CalculateCalorieTarget(decimal tdee, GoalType goalType, Gender gender, decimal currentWeight, decimal targetWeight, int durationWeeks, decimal heightCm = 0);
    MacroRecommendation CalculateRecommendedMacros(int targetCalories, DietType dietType, GoalType goalType,
        decimal weightKg, DietaryConstraints? constraints = null);
}
