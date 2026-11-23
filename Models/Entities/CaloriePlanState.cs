using System.Text.Json;

namespace NutriTrackAI.Models.Entities;

// Stored in the existing goals table. A typed, versioned snapshot preserves consent across requests.
public class CaloriePlanState
{
    public int Version { get; set; } = 1;
    public decimal Adjustment { get; set; }
    public bool Introductory { get; set; }
    public DateTime LastReviewedOn { get; set; }
    public DateTime? LastBodyCheckInOn { get; set; }
    public bool RequiresPlanReconfirmation { get; set; }
    public string Reason { get; set; } = "Khởi tạo kế hoạch đã xác nhận.";
    public List<CaloriePlanChange> Changes { get; set; } = new();
    public static CaloriePlanState? Read(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<CaloriePlanState>(json);
    public string ToJson() => JsonSerializer.Serialize(this);
}

public record CaloriePlanChange(DateTime On, decimal Adjustment, string Reason, decimal? PreviousAdjustment = null);
