namespace NutriTrackAI.Tests;

// Shared fixture at the same 0.1 kg precision as the application's persisted weights.
public static class QaWeightData
{
    public static IReadOnlyList<decimal> Weights { get; } = Array.AsReadOnly(new[] {
        72.0m, 72.1m, 72.0m, 72.0m, 71.9m, 72.0m, 72.0m,
        71.9m, 72.0m, 71.9m, 71.9m, 72.0m, 71.9m, 71.9m,
        71.9m, 72.0m, 71.9m, 71.8m, 71.9m, 71.8m, 71.9m,
        71.8m, 71.9m, 71.8m, 71.7m, 71.8m, 71.7m, 71.7m
    });
}
