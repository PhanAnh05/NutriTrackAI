using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;

namespace NutriTrackAI.Models.ViewModels;

public sealed class HealthScreeningForm : IValidatableObject
{
    public bool NoConditions { get; set; }
    public MedicalConditions[] Conditions { get; set; } = [];
    public bool DoctorAwareOfPlan { get; set; }

    public bool HasAnswer => NoConditions || Conditions is { Length: > 0 };

    private string? ValidationMessage =>
        Conditions == null || Conditions.Any(value => value == MedicalConditions.None || !Enum.IsDefined(value))
            ? "Tình trạng sức khỏe không hợp lệ."
            : NoConditions && Conditions.Length > 0
                ? "Chỉ chọn không có bệnh nền khi bạn không chọn tình trạng nào khác." : null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValidationMessage is { } error) yield return new ValidationResult(error);
    }

    public bool ApplyTo(UserProfile profile)
    {
        if (!HasAnswer || ValidationMessage != null) return false;
        var updated = Conditions.Aggregate(MedicalConditions.None, (flags, condition) => flags | condition);
        var needsDoctorChanged = profile.HealthScreeningAt.HasValue &&
            GoalPolicy.ConditionsRequiringDoctorAwareness(profile.MedicalConditions) != GoalPolicy.ConditionsRequiringDoctorAwareness(updated);
        profile.MedicalConditions = updated;
        profile.DoctorAwareOfPlan = !needsDoctorChanged && DoctorAwareOfPlan && GoalPolicy.RequiresDoctorAwareness(updated);
        profile.HealthScreeningAt = GoalPolicy.Now;
        return needsDoctorChanged && GoalPolicy.RequiresDoctorAwareness(updated);
    }

    public static HealthScreeningForm From(UserProfile profile) => new()
    {
        NoConditions = profile.HealthScreeningAt.HasValue && profile.MedicalConditions == MedicalConditions.None,
        Conditions = GoalPolicy.HealthConditionOptions.Where(option => profile.MedicalConditions.HasFlag(option.Condition))
            .Select(option => option.Condition).ToArray(),
        DoctorAwareOfPlan = profile.DoctorAwareOfPlan
    };
}
