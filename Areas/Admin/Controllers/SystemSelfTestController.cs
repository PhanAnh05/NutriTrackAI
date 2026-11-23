using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SystemSelfTestController(INutritionCalculatorService calculator, IChatSafetyFilter safety) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Synthetic inputs only: never query an individual's profile, logs or chat for an admin report.
        var group = new TestGroupResult { GroupName = "Kiểm tra công thức trên dữ liệu giả lập",
            Description = "Không truy vấn hồ sơ hay nhật ký cá nhân. Kiểm thử tích hợp nằm trong NutriTrackAI.Tests.", Icon = "bi-calculator" };
        void Check(string id, string name, string input, decimal actual, decimal expected, decimal tolerance = 0.01m)
            => group.TestCases.Add(new TestCaseResult { Id = id, Name = name, Category = "Công thức",
                InputDescription = input, ExpectedOutput = expected.ToString("0.###"), ActualOutput = actual.ToString("0.###"), Passed = Math.Abs(actual - expected) <= tolerance });
        Check("BMI", "BMI", $"72 kg, 170 cm. {GoalPolicy.AdultBmiReference}", calculator.CalculateBmi(72, 170), 24.913m);
        Check("BMR", "Mifflin–St Jeor", "Nam, 24 tuổi, 72 kg, 170 cm", calculator.CalculateBmr(Gender.Male, 72, 170, 24), 1667.5m);
        Check("TDEE", "TDEE", "BMR 1667.5, Moderate", calculator.CalculateTdee(1667.5m, ActivityLevel.Moderate), 2584.625m);
        foreach (var weeks in new[] { 9, 11, 14, 16 })
        {
            var result = GoalPlanningService.AnalyzeEnergy(2584.625m, GoalType.Lose, Gender.Male, 72, 65, weeks, 170);
            var expectedBlocked = weeks <= 11;
            group.TestCases.Add(new TestCaseResult { Id = $"PLAN-{weeks}", Name = $"Giảm 7 kg / {weeks} tuần",
                Category = "Mục tiêu", InputDescription = "Hồ sơ giả lập", ExpectedOutput = expectedBlocked ? "Cần điều chỉnh" : "Trong giới hạn hồ sơ",
                ActualOutput = $"{result.RequiredDailyAdjustment:0.##} kcal/ngày · {result.StatusLabel}", Passed = result.IsBlocked == expectedBlocked });
        }
        var risk = safety.CheckSafety("Tôi muốn dùng thuốc giảm cân");
        group.TestCases.Add(new TestCaseResult { Id = "FILTER", Name = "Bộ lọc y tế", Category = "Chatbot", InputDescription = "Câu hỏi thuốc giảm cân giả lập",
            ExpectedOutput = "Có rủi ro y tế", ActualOutput = risk.IsMedicalRisk.ToString(), Passed = risk.IsMedicalRisk });
        var vm = new SystemSelfTestViewModel();
        vm.Groups.Add(group);
        vm.TotalTests = group.TotalCount;
        vm.PassedTests = group.PassedCount;
        vm.FailedTests = vm.TotalTests - vm.PassedTests;
        return View(vm);
    }
}
