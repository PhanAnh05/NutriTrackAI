using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public sealed record DietaryConstraints
{
    public static DietaryConstraints None { get; } = new();
    public bool RequireMedicalDisclaimer => true;
    public int? MinimumMeals { get; init; }
    public bool NoIntermittentFasting { get; init; }
    public decimal? MinCarbRatio { get; init; }
    public decimal? MaxCarbRatio { get; init; }
    public bool DistributeCarbs { get; init; }
    public int? MaxSodiumMg { get; init; }
    public decimal? MaxProteinGramsPerKg { get; init; }
    public bool NoKeto { get; init; }
    public bool WaterPerDoctor { get; init; }
    public bool SoyMedicationTiming { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public static partial class GoalPolicy
{
    public const int GastritisMinimumMeals = 4;
    public const int HeartSodiumLimitMg = 2000;
    public const decimal DiabetesMinCarbRatio = .40m;
    public const decimal DiabetesMaxCarbRatio = .45m;
    public const decimal GoutMaxProteinGramsPerKg = 1.2m;
    public const decimal MaxFatRatio = .35m;
    public const string PotassiumNote = "Một số bệnh thận hoặc thuốc huyết áp cần hạn chế kali, hãy hỏi bác sĩ trước khi tăng thực phẩm giàu kali.";
    public const string SoyTimingNote = "Đậu nành nên cách xa giờ uống thuốc tuyến giáp theo hướng dẫn bác sĩ.";
    public const string WaterPerDoctorNote = "Hãy dùng mức nước theo hướng dẫn bác sĩ.";
    public const string MedicalDisclaimer = "Thông tin chỉ mang tính tham khảo, không thay thế tư vấn y tế.";
    public const string IncompatibleMacros = "Các giới hạn dinh dưỡng hiện tại không thể cùng đáp ứng ngân sách calo. Hãy hỏi bác sĩ hoặc chuyên gia dinh dưỡng về tỷ lệ phù hợp.";
    public const string NoAcidicSpicyTag = "no_acidic_spicy";
    public const string LowGiTag = "low_gi";
    public const string LowSodiumTag = "low_sodium";
    public const string NoPurineRichTag = "no_purine_rich";

    private static DietaryConstraints ConditionDiet(MedicalConditions conditions)
    {
        var gastritis = conditions.HasFlag(MedicalConditions.Gastritis);
        var diabetes = conditions.HasFlag(MedicalConditions.Diabetes);
        var lowSodium = (conditions & (MedicalConditions.Hypertension | MedicalConditions.HeartDisease)) != 0;
        var gout = conditions.HasFlag(MedicalConditions.Gout);
        var tags = new List<string>();
        if (gastritis) tags.Add(NoAcidicSpicyTag);
        if (diabetes) tags.Add(LowGiTag);
        if (lowSodium) tags.Add(LowSodiumTag);
        if (gout) tags.Add(NoPurineRichTag);

        return new()
        {
            MinimumMeals = gastritis ? GastritisMinimumMeals : null,
            NoIntermittentFasting = gastritis,
            MinCarbRatio = diabetes ? DiabetesMinCarbRatio : null,
            MaxCarbRatio = diabetes ? DiabetesMaxCarbRatio : null,
            DistributeCarbs = diabetes,
            MaxSodiumMg = lowSodium ? HeartSodiumLimitMg : null,
            MaxProteinGramsPerKg = gout ? GoutMaxProteinGramsPerKg : null,
            NoKeto = gout,
            WaterPerDoctor = gout && (conditions & (MedicalConditions.HeartDisease | MedicalConditions.KidneyDisease)) != 0,
            SoyMedicationTiming = conditions.HasFlag(MedicalConditions.Hypothyroidism),
            Tags = tags.AsReadOnly()
        };
    }

    public static MacroRecommendation ApplyConditionMacros(decimal calories, decimal weightKg,
        (decimal Protein, decimal Carb, decimal Fat) baseRatios, DietaryConstraints constraints)
    {
        if (calories <= 0) return new();

        var proteinLimit = constraints.MaxProteinGramsPerKg is decimal gramsPerKg
            ? Math.Min(calories, gramsPerKg * weightKg * 4m) : calories;
        var protein = Math.Min(calories * baseRatios.Protein, proteinLimit);
        var targetFat = calories * Math.Min(baseRatios.Fat, MaxFatRatio);
        var carb = calories - protein - targetFat;
        var maxFat = calories * MaxFatRatio;

        if (constraints.MinCarbRatio is decimal minCarbRatio && constraints.MaxCarbRatio is decimal maxCarbRatio)
        {
            var maximumCarb = calories * maxCarbRatio;
            carb = Math.Clamp(carb, calories * minCarbRatio, maximumCarb);
            if (!constraints.MaxProteinGramsPerKg.HasValue)
            {
                // Share energy displaced by the carb bound in the original protein-to-fat proportion.
                var nonCarb = baseRatios.Protein + baseRatios.Fat;
                protein = nonCarb > 0 ? (calories - carb) * baseRatios.Protein / nonCarb : calories - carb;
            }
            // Use room in the carb band and then room under the protein cap to preserve the fat ceiling.
            if (calories - protein - carb > maxFat)
            {
                carb = Math.Min(maximumCarb, Math.Max(carb, calories - protein - maxFat));
                protein = Math.Min(proteinLimit, Math.Max(protein, calories - carb - maxFat));
            }
        }

        var fat = calories - protein - carb;
        if (fat > maxFat) return new() { UnavailableReason = IncompatibleMacros };

        var proteinPercent = RoundCalories(100m * protein / calories);
        var fatPercent = RoundCalories(100m * fat / calories);
        return new()
        {
            ProteinGrams = protein / 4m,
            CarbGrams = carb / 4m,
            FatGrams = fat / 9m,
            ProteinPercent = proteinPercent,
            CarbPercent = 100 - proteinPercent - fatPercent,
            FatPercent = fatPercent
        };
    }
}
