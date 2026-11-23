using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public static class ExerciseEnergy
{
    // Product estimates relative to the catalogue MET at Moderate intensity.
    // A custom activity uses a baseline of 5 MET, preserving the 3/5/8 presets.
    public static decimal EffectiveMet(decimal baseMet, ExerciseIntensity intensity) =>
        baseMet * (intensity switch
        {
            ExerciseIntensity.Low => 0.6m,
            ExerciseIntensity.Moderate => 1m,
            ExerciseIntensity.High => 1.6m,
            _ => throw new ArgumentOutOfRangeException(nameof(intensity))
        });

    public static decimal NetCalories(decimal met, decimal weightKg, int durationMin) =>
        Math.Round(Math.Max(met - 1m, 0m) * 3.5m * weightKg / 200m * durationMin,
            1, MidpointRounding.AwayFromZero);
}
