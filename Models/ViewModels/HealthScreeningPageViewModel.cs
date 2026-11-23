namespace NutriTrackAI.Models.ViewModels;

public sealed class HealthScreeningPageViewModel
{
    public HealthScreeningForm Screening { get; set; } = new();
    public string? ReturnUrl { get; set; }
}
