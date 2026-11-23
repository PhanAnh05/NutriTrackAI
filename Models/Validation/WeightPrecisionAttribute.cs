using System.ComponentModel.DataAnnotations;

namespace NutriTrackAI.Models.Validation;

public sealed class WeightPrecisionAttribute : ValidationAttribute
{
    public WeightPrecisionAttribute() : base("Cân nặng chỉ dùng tối đa một chữ số thập phân.") { }
    public override bool IsValid(object? value) => value == null || value is decimal weight && Math.Round(weight, 1) == weight;
}
