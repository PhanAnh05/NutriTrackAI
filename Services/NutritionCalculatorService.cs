using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public class NutritionCalculatorService : INutritionCalculatorService
{
    public int CalculateAge(DateTime dob) => GoalPolicy.AgeOn(dob, GoalPolicy.Today);

    public decimal CalculateBmi(decimal weightKg, decimal heightCm)
    {
        if (heightCm <= 0 || weightKg <= 0) return 0;
        var heightM = heightCm / 100m;
        return weightKg / (heightM * heightM);
    }

    public string GetBmiCategory(decimal bmi)
    {
        if (bmi < GoalPolicy.MinimumAdultBmi) return "Thiếu cân";
        if (bmi < GoalPolicy.OverweightBmi) return "Bình thường";
        if (bmi < GoalPolicy.ObesityBmi) return "Thừa cân";
        return "Béo phì";
    }

    public BmiResult GetBmiInfo(decimal weightKg, decimal heightCm, int? age = null)
    {
        var bmi = CalculateBmi(weightKg, heightCm);
        var category = age < 18 ? "Cần đánh giá BMI theo tuổi; không dùng phân loại người lớn" : GetBmiCategory(bmi);
        var badgeClass = category switch
        {
            "Thiếu cân" => "badge-custom-warning",
            "Bình thường" => "badge-custom-emerald",
            "Thừa cân" => "bg-warning-subtle text-warning-emphasis border border-warning",
            "Béo phì" => "bg-danger-subtle text-danger border border-danger",
            _ => "bg-secondary-subtle text-secondary"
        };

        return new BmiResult
        {
            Bmi = bmi,
            Category = category,
            BadgeClass = badgeClass
        };
    }

    public decimal CalculateBmr(Gender gender, decimal weightKg, decimal heightCm, int age)
    {
        // Công thức Mifflin-St Jeor:
        // Nam: 10 * W + 6.25 * H - 5 * A + 5
        // Nữ: 10 * W + 6.25 * H - 5 * A - 161
        // Khác: trung bình cộng Nam & Nữ (-78)
        return 10m * weightKg + 6.25m * heightCm - 5m * age + (gender switch
        {
            Gender.Male => 5m,
            Gender.Female => -161m,
            _ => -78m // Project convention, not a separately validated clinical equation.
        });
    }

    public decimal CalculateTdee(decimal bmr, ActivityLevel activityLevel)
    {
        var factor = activityLevel switch
        {
            ActivityLevel.Sedentary => 1.2m,
            ActivityLevel.Light => 1.375m,
            ActivityLevel.Moderate => 1.55m,
            ActivityLevel.Active => 1.725m,
            ActivityLevel.VeryActive => 1.9m,
            _ => 1.2m
        };

        return bmr * factor;
    }

    public void GetRecommendedDuration(
        GoalType goalType,
        decimal currentWeight,
        decimal targetWeight,
        out int minWeeks,
        out int maxWeeks,
        out int recommendedWeeks)
    {
        var adjustment = goalType == GoalType.Lose ? GoalPolicy.MaxDeficitHighBmi : GoalPolicy.MaximumSurplus;
        var energy = goalType == GoalType.Maintain ? 0 : GoalPolicy.EstimateEnergy(targetWeight - currentWeight);
        minWeeks = energy == 0 ? 4 : (int)Math.Ceiling(energy / adjustment / 7m);
        var usual = goalType == GoalType.Lose ? GoalPolicy.UsualDeficit : GoalPolicy.MaximumSurplus;
        recommendedWeeks = energy == 0 ? 4 : (int)Math.Ceiling(energy / usual / 7m);
        maxWeeks = Math.Max(recommendedWeeks, recommendedWeeks * 2);
    }

    public void GetBmiWeightLimits(decimal heightCm, out decimal minSafeWeight, out decimal maxNormalWeight, out decimal maxAllowedWeight)
    {
        (minSafeWeight, maxNormalWeight, maxAllowedWeight) = GoalPolicy.BmiWeightLimits(heightCm);
    }

    public CalorieTargetResult CalculateCalorieTarget(decimal tdee, GoalType goalType, Gender gender,
        decimal currentWeight, decimal targetWeight, int durationWeeks, decimal heightCm = 0)
        => GoalPlanningService.AnalyzeEnergy(tdee, goalType, gender, currentWeight, targetWeight, durationWeeks, heightCm);

    public MacroRecommendation CalculateRecommendedMacros(
        int targetCalories,
        DietType dietType,
        GoalType goalType,
        decimal weightKg,
        DietaryConstraints? constraints = null)
    {
        int pPercent, cPercent, fPercent;

        switch (dietType)
        {
            case DietType.HighProtein:
                pPercent = 35;
                cPercent = 35;
                fPercent = 30;
                break;

            case DietType.LowCarb:
                pPercent = 30;
                cPercent = 25; // < 30% năng lượng từ carb
                fPercent = 45;
                break;

            case DietType.Vegetarian:
                pPercent = 20;
                cPercent = 55;
                fPercent = 25;
                break;

            case DietType.Normal:
            default:
                if (goalType == GoalType.Lose)
                {
                    pPercent = 30;
                    cPercent = 40;
                    fPercent = 30;
                }
                else if (goalType == GoalType.Gain)
                {
                    pPercent = 25;
                    cPercent = 50;
                    fPercent = 25;
                }
                else
                {
                    pPercent = 20;
                    cPercent = 50;
                    fPercent = 30;
                }
                break;
        }

        return GoalPolicy.ApplyConditionMacros(targetCalories, weightKg,
            (pPercent / 100m, cPercent / 100m, fPercent / 100m), constraints ?? DietaryConstraints.None);
    }
}
