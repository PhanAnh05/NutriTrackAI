using NutriTrackAI.Models.Entities;
using NutriTrackAI.Services;

namespace NutriTrackAI.Models.ViewModels;

public record WeightMismatchViewModel(decimal ProfileWeight, decimal LogWeight, DateTime LogDate)
{
    public static WeightMismatchViewModel? From(decimal profileWeight, WeightLog? latest)
    {
        return latest is null || WeightTrackingService.SameWeight(profileWeight, latest.WeightKg)
            ? null
            : new WeightMismatchViewModel(profileWeight, latest.WeightKg, latest.LoggedOn.Date);
    }
}
