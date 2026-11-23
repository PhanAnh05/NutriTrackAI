using NutriTrackAI.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Models.ViewModels;

public class WeightLogIndexViewModel
{
    public List<WeightLog> Logs { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public decimal CurrentWeight { get; set; }
    public WeightMismatchViewModel? WeightMismatch { get; set; }
    public decimal? TargetWeight { get; set; }
    public decimal? StartWeight { get; set; }
    public decimal WeightChange { get; set; }
    public decimal Bmi { get; set; }
    public string BmiCategory { get; set; } = string.Empty;
    public string BmiBadgeClass { get; set; } = string.Empty;

    // Form nhập/chỉnh sửa
    public WeightLogFormModel Form { get; set; } = new();

    // Dữ liệu cho Chart.js
    public List<string> ChartDates { get; set; } = new();
    public List<decimal> ChartWeights { get; set; } = new();
    public List<decimal> ChartTargets { get; set; } = new();
}

public class WeightLogFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày ghi nhận.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày ghi nhận")]
    public DateTime LoggedOn { get; set; } = GoalPolicy.Today;

    [Required(ErrorMessage = "Vui lòng nhập cân nặng.")]
    [Range(20, 300, ErrorMessage = "Cân nặng phải từ 20 đến 300 kg.")]
    [Display(Name = "Cân nặng (kg)")]
    [NutriTrackAI.Models.Validation.WeightPrecision]
    public decimal WeightKg { get; set; } = 70m;

    public bool ConfirmLargeWeightChange { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú tiến độ")]
    public string? Note { get; set; }

    [Display(Name = "Ảnh tiến độ (tùy chọn)")]
    public IFormFile? PhotoFile { get; set; }

    public string? ExistingPhotoUrl { get; set; }
}
