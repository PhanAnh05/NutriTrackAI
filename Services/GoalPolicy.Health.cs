using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public enum HealthTier { Clear, Caution, NeedsDoctor, Blocked }

public sealed record HealthAssessment(HealthTier Tier, IReadOnlyList<string> Reasons,
    bool NeedsScreening, string? SuspensionReason, string? PublicSuspensionReason)
{
    public DietaryConstraints Constraints { get; init; } = DietaryConstraints.None;
}

public sealed record HealthConditionOption(MedicalConditions Condition, string Label, HealthTier Tier);

public static partial class GoalPolicy
{
    public const decimal DoctorAwareAdjustmentCap = 350m;
    public const string CompleteHealthScreening = "Hãy hoàn thành sàng lọc sức khỏe.";
    public const string HealthNotesHelp = "Ghi chú không dùng để lập kế hoạch, hãy chọn tình trạng sức khỏe trong danh sách.";
    public const string HealthScreeningDisclaimer = "Thông tin này dùng để chọn mức an toàn. Ứng dụng không chẩn đoán hay thay thế bác sĩ.";
    public const string HealthScreeningPrompt = "Bạn có thể cập nhật sàng lọc sức khỏe để hệ thống chọn mức phù hợp với hồ sơ.";
    private const string ProfessionalReview = "Hãy gặp bác sĩ hoặc chuyên gia dinh dưỡng trước khi thay đổi kế hoạch calo.";
    private const string DoctorAwarenessRequired = "Cần bác sĩ xác nhận. Hãy xác nhận bác sĩ đã biết kế hoạch giảm hoặc tăng cân của bạn.";

    public static IReadOnlyList<HealthConditionOption> HealthConditionOptions { get; } = Array.AsReadOnly<HealthConditionOption>(
    [
        new(MedicalConditions.Diabetes, "Tiểu đường", HealthTier.NeedsDoctor),
        new(MedicalConditions.UsesInsulinOrGlucoseLowering, "Đang dùng insulin hoặc thuốc hạ đường huyết", HealthTier.Blocked),
        new(MedicalConditions.Gastritis, "Dạ dày (viêm loét, trào ngược)", HealthTier.Caution),
        new(MedicalConditions.Hypertension, "Cao huyết áp", HealthTier.NeedsDoctor),
        new(MedicalConditions.HeartDisease, "Bệnh tim mạch", HealthTier.NeedsDoctor),
        new(MedicalConditions.Gout, "Gout hoặc axit uric cao", HealthTier.NeedsDoctor),
        new(MedicalConditions.Hypothyroidism, "Tuyến giáp (suy giáp)", HealthTier.Caution),
        new(MedicalConditions.KidneyDisease, "Bệnh thận mạn", HealthTier.Blocked),
        new(MedicalConditions.EatingDisorderHistory, "Từng có rối loạn ăn uống", HealthTier.Blocked),
        new(MedicalConditions.PregnantOrBreastfeeding, "Đang mang thai hoặc cho con bú", HealthTier.Blocked),
        new(MedicalConditions.OtherCondition, "Bệnh khác", HealthTier.Blocked)
    ]);

    private static readonly MedicalConditions KnownConditions = HealthConditionOptions
        .Aggregate(MedicalConditions.None, (flags, option) => flags | option.Condition);

    private static readonly MedicalConditions RequiresDoctorMask = HealthConditionOptions
        .Where(option => option.Tier == HealthTier.NeedsDoctor)
        .Aggregate(MedicalConditions.None, (flags, option) => flags | option.Condition);

    public static MedicalConditions ConditionsRequiringDoctorAwareness(MedicalConditions conditions) =>
        conditions & RequiresDoctorMask;

    public static bool RequiresDoctorAwareness(MedicalConditions conditions) =>
        ConditionsRequiringDoctorAwareness(conditions) != MedicalConditions.None;

    internal static int AgeOn(DateTime birthDate, DateTime date)
    {
        var age = date.Year - birthDate.Year;
        if (birthDate.Date > date.AddYears(-age)) age--;
        return Math.Max(0, age);
    }

    public static HealthAssessment EvaluateHealth(UserProfile profile) => EvaluateHealth(profile, AgeOn(profile.DateOfBirth, Today));

    internal static HealthAssessment EvaluateHealth(UserProfile profile, int age)
    {
        var needsScreening = !profile.HealthScreeningAt.HasValue;
        var conditions = profile.MedicalConditions;
        var constraints = needsScreening ? DietaryConstraints.None : ConditionDiet(conditions & KnownConditions);
        if (age < 18)
        {
            const string minor = "Người dưới 18 tuổi cần đánh giá dinh dưỡng phù hợp độ tuổi; hệ thống không lập kế hoạch calo người lớn.";
            return new(HealthTier.Blocked, [minor], needsScreening, $"{minor} {ProfessionalReview}", ProfessionalReview) { Constraints = constraints };
        }

        if (needsScreening)
        {
            // An older explicit risk declaration remains restricted until the user screens again.
            var legacyRisk = HealthScreening.StatusOf(profile.HealthScreeningJson) == ScreeningStatus.NeedsReview;
            if (legacyRisk || !string.IsNullOrWhiteSpace(profile.HealthNotes))
                return new(HealthTier.Blocked, [CompleteHealthScreening], true, CompleteHealthScreening, CompleteHealthScreening);
            return new(HealthTier.Clear, [], true, null, null);
        }

        if ((conditions & ~KnownConditions) != 0)
            return new(HealthTier.Blocked, [CompleteHealthScreening], true, CompleteHealthScreening, CompleteHealthScreening);

        var selected = HealthConditionOptions.Where(option => conditions.HasFlag(option.Condition)).ToArray();
        var tier = selected.Length == 0 ? HealthTier.Clear : selected.Max(option => option.Tier);
        var reasons = selected.Select(option => option.Label).ToList();
        if (tier == HealthTier.Blocked)
        {
            reasons.Add(ProfessionalReview);
            return new(tier, reasons, false, string.Join(". ", reasons), ProfessionalReview) { Constraints = constraints };
        }
        if (tier == HealthTier.NeedsDoctor)
        {
            if (!profile.DoctorAwareOfPlan)
            {
                reasons.Add(DoctorAwarenessRequired);
                return new(tier, reasons, false, string.Join(". ", reasons), DoctorAwarenessRequired) { Constraints = constraints };
            }
            reasons.Add("Hãy cân nhắc giai đoạn làm quen để theo dõi đáp ứng trước khi điều chỉnh thêm.");
        }
        return new(tier, reasons, false, null, null) { Constraints = constraints };
    }
}
