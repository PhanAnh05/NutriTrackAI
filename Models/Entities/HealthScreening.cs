using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NutriTrackAI.Models.Entities;

public enum ScreeningAnswer { No = 1, Yes, Unsure }
public enum ScreeningStatus { NotScreened, NoReportedFactors, NeedsReview }

public sealed record HealthScreeningAnswers
{
    [EnumDataType(typeof(ScreeningAnswer))]
    public ScreeningAnswer? PregnancyOrBreastfeeding { get; init; }
    [EnumDataType(typeof(ScreeningAnswer))]
    public ScreeningAnswer? ConditionOrTreatment { get; init; }
    [EnumDataType(typeof(ScreeningAnswer))]
    public ScreeningAnswer? EatingDisorder { get; init; }
    [EnumDataType(typeof(ScreeningAnswer))]
    public ScreeningAnswer? UnexpectedWeightLossOrSymptoms { get; init; }

    public ScreeningStatus Evaluate()
    {
        ScreeningAnswer?[] answers = [PregnancyOrBreastfeeding, ConditionOrTreatment, EatingDisorder, UnexpectedWeightLossOrSymptoms];
        if (answers.Any(answer => answer.HasValue && answer != ScreeningAnswer.No))
            return ScreeningStatus.NeedsReview;
        return answers.All(answer => answer == ScreeningAnswer.No)
            ? ScreeningStatus.NoReportedFactors : ScreeningStatus.NotScreened;
    }
}

public sealed record HealthScreening(int Version, DateTimeOffset UpdatedAt, HealthScreeningAnswers Answers)
{
    private const int CurrentVersion = 1;

    public static HealthScreening? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var screening = JsonSerializer.Deserialize<HealthScreening>(json);
            return screening is { Version: CurrentVersion, Answers: not null } && screening.UpdatedAt != default
                ? screening : null;
        }
        catch (JsonException) { return null; }
    }

    public static ScreeningStatus StatusOf(string? json) =>
        Read(json)?.Answers.Evaluate() ?? ScreeningStatus.NotScreened;

    public static string? Update(string? existing, HealthScreeningAnswers? answers)
    {
        if (answers == null || Read(existing)?.Answers == answers) return existing;
        return JsonSerializer.Serialize(new HealthScreening(CurrentVersion, DateTimeOffset.UtcNow, answers));
    }
}
