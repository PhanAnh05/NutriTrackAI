using NutriTrackAI.Services;
using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class WaterExerciseIndexViewModel
{
    [DataType(DataType.Date)]
    public DateTime SelectedDate { get; set; } = GoalPolicy.Today;

    // Nước
    public List<WaterLog> WaterLogs { get; set; } = new();
    public int TotalWaterMl { get; set; }
    public int DailyWaterGoalMl { get; set; } = 2000;
    public int WaterProgressPercent { get; set; }

    // Vận động
    public List<ExerciseLog> ExerciseLogs { get; set; } = new();
    public int TotalExerciseDurationMin { get; set; }
    public decimal TotalCaloriesBurned { get; set; }
    public List<Exercise> AvailableExercises { get; set; } = new();

    // User weight để tính calo
    public decimal UserWeightKg { get; set; } = 70m;

    // Forms
    public AddWaterLogModel WaterForm { get; set; } = new();
    public AddExerciseLogModel ExerciseForm { get; set; } = new();
}

public class AddWaterLogModel
{
    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; } = GoalPolicy.Today;

    [Required(ErrorMessage = "Vui lòng nhập lượng nước.")]
    [Range(50, 2000, ErrorMessage = "Lượng nước mỗi lần từ 50 đến 2.000 ml.")]
    [Display(Name = "Lượng nước (ml)")]
    public int AmountMl { get; set; } = 250;
}

public class UpdateWaterGoalModel
{
    [Required]
    [Range(500, 10000, ErrorMessage = "Mục tiêu nước từ 500 đến 10.000 ml.")]
    public int DailyWaterGoalMl { get; set; } = 2000;
}

public class AddExerciseLogModel
{
    [Required]
    [DataType(DataType.Date)]
    public DateTime LoggedOn { get; set; } = GoalPolicy.Today;

    [Display(Name = "Bài tập từ danh mục")]
    public int? ExerciseId { get; set; }

    [StringLength(120)]
    [Display(Name = "Hoặc tên hoạt động tự chọn")]
    public string? CustomName { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập thời gian vận động.")]
    [Range(1, 300, ErrorMessage = "Thời gian vận động từ 1 đến 300 phút.")]
    [Display(Name = "Thời gian (phút)")]
    public int DurationMin { get; set; } = 30;

    [Required]
    [Display(Name = "Cường độ")]
    public ExerciseIntensity Intensity { get; set; } = ExerciseIntensity.Moderate;
}
