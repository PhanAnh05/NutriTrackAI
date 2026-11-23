using NutriTrackAI.Services;
namespace NutriTrackAI.Areas.Admin.Models;

public class SystemSelfTestViewModel
{
    public DateTime ExecutedAt { get; set; } = GoalPolicy.Now;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public long ExecutionTimeMs { get; set; }

    public List<TestGroupResult> Groups { get; set; } = new();
}

public class TestGroupResult
{
    public string GroupName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-check2-circle";
    public List<TestCaseResult> TestCases { get; set; } = new();

    public int PassedCount => TestCases.Count(t => t.Passed);
    public int TotalCount => TestCases.Count;
    public bool AllPassed => TestCases.All(t => t.Passed);
}

public class TestCaseResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string InputDescription { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public string ActualOutput { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Details { get; set; }
}
