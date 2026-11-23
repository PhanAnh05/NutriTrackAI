using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public record CalorieBudget(Goal? Goal, decimal Tdee, decimal Adjustment, int TargetCalories,
    string Phase, string Explanation, DateTime? EstimatedDate, DateTime? NextReviewOn, bool Suspended, decimal MaximumAdjustment)
{
    public const string PlanLabel = "Ngân sách calo";
    public const string MaintenanceLabel = "Mốc duy trì (TDEE)";
    public bool UsesMaintenanceReference => Suspended || Goal == null;
    public string ReferenceLabel => UsesMaintenanceReference ? MaintenanceLabel : PlanLabel;
    public bool BodyCheckInDue { get; init; }
    public HealthAssessment Health { get; init; } = new(HealthTier.Clear, [], false, null, null);
}

public class CalorieBudgetService(AppDbContext db, INutritionCalculatorService calculator, GoalPlanningService planner)
{
    public async Task<CalorieBudget> GetAsync(int userId)
    {
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId)
            ?? throw new InvalidOperationException("Hãy hoàn tất hồ sơ trước khi lập kế hoạch.");
        var goal = await db.Goals.Where(g => g.UserId == userId && g.IsActive)
            .OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();
        return Calculate(profile, goal);
    }

    public CaloriePlanState StateFor(Goal goal)
    {
        return CaloriePlanState.Read(goal.CaloriePlanJson) ?? new CaloriePlanState
        {
            Adjustment = goal.GoalType == GoalType.Maintain ? 0 : Math.Abs(goal.WeeklyRateKg) * GoalPolicy.EnergyPerKg / 7m,
            LastReviewedOn = goal.StartDate,
            Reason = "Kế hoạch cũ: mức điều chỉnh ước tính từ tốc độ đã lưu, cần đánh giá lại."
        };
    }

    public MacroRecommendation? GetMacros(UserProfile profile, CalorieBudget budget) => budget.Suspended
        ? null
        : calculator.CalculateRecommendedMacros(budget.TargetCalories, profile.DietType,
            budget.Goal?.GoalType ?? GoalType.Maintain, profile.CurrentWeightKg, budget.Health.Constraints);

    public CalorieBudget Calculate(UserProfile profile, Goal? goal)
    {
        var age = calculator.CalculateAge(profile.DateOfBirth);
        var health = GoalPolicy.EvaluateHealth(profile, age);
        var tdee = calculator.CalculateTdee(calculator.CalculateBmr(profile.Gender, profile.CurrentWeightKg,
            profile.HeightCm, age), profile.ActivityLevel);
        if (goal == null)
        {
            var restriction = GoalPlanningService.ScopeRestriction(profile, age, health);
            return new(null, tdee, 0, GoalPolicy.RoundCalories(tdee), "Theo dõi",
                restriction ?? "Chưa có mục tiêu đang hoạt động; TDEE chỉ dùng làm mốc tham khảo.",
                null, null, restriction != null, 0m) { Health = health };
        }
        var state = StateFor(goal);
        var allowed = planner.CanApply(profile, goal.GoalType, goal.TargetWeight, state.Adjustment, out var reason, goal.StartWeight);
        if (allowed && state.RequiresPlanReconfirmation)
        {
            allowed = false;
            reason = "Kế hoạch cũ vẫn tạm ngừng sau khi cập nhật sàng lọc. Hãy xem và xác nhận một mục tiêu mới nếu phù hợp.";
        }
        var adjustment = allowed ? state.Adjustment : 0;
        var target = GoalPolicy.RoundCalories(tdee + (goal.GoalType == GoalType.Lose ? -adjustment : adjustment));
        var phase = !allowed ? "Tạm ngừng điều chỉnh năng lượng" : goal.GoalType == GoalType.Maintain ? "Duy trì" : adjustment == 0m ? "Tạm dừng điều chỉnh năng lượng" : state.Introductory ? "Làm quen · chờ đánh giá" : goal.GoalType == GoalType.Lose ? "Giảm cân ổn định" : "Tăng cân ổn định";
        var remaining = goal.GoalType == GoalType.Lose ? Math.Max(0, profile.CurrentWeightKg - goal.TargetWeight) : Math.Max(0, goal.TargetWeight - profile.CurrentWeightKg);
        var today = GoalPolicy.Today;
        DateTime? estimate = goal.GoalType == GoalType.Maintain ? null :
            GoalPolicy.EstimateTargetDate(remaining, adjustment, today);
        var explanation = allowed
            ? $"{state.Reason} TDEE được tính lại theo hồ sơ hiện tại; giữ mức điều chỉnh đã xác nhận {adjustment:0.##} kcal/ngày. Ngày dự kiến giả định tiếp tục mức hiện tại, không cam kết kết quả."
            : $"{reason} Ngân sách hiển thị tạm dùng TDEE, không tiếp tục thâm hụt/thặng dư cũ. Hãy đánh giá lại kế hoạch.";
        return new(goal, tdee, adjustment, target, phase, explanation, estimate,
            state.LastReviewedOn.Date.AddDays(GoalPolicy.ReviewDays), !allowed,
            allowed ? GoalPolicy.MaximumAdjustment(goal.GoalType, tdee, profile.Gender, profile.CurrentWeightKg,
                profile.HeightCm, goal.StartWeight, health: health) : 0m)
        {
            BodyCheckInDue = GoalPlanningService.IsBodyCheckInDue(goal, state, today),
            Health = health
        };
    }

    public void ReevaluateAfterProfileUpdate(UserProfile profile, Goal goal, bool previouslyRestricted)
    {
        if (goal.GoalType != GoalType.Maintain && (previouslyRestricted ||
            GoalPlanningService.ScopeRestriction(profile, calculator.CalculateAge(profile.DateOfBirth)) != null))
        {
            var state = StateFor(goal);
            state.RequiresPlanReconfirmation = true;
            goal.CaloriePlanJson = state.ToJson();
        }
        goal.DailyCalorieTarget = Calculate(profile, goal).TargetCalories;
    }

    // Called only from explicit profile/weight POSTs. A recalculated TDEE never increases the agreed deficit.
    public async Task<string?> RefreshAsync(int userId)
    {
        var budget = await GetAsync(userId);
        if (budget.Goal == null) return null;
        var old = budget.Goal.DailyCalorieTarget;
        budget.Goal.DailyCalorieTarget = budget.TargetCalories;
        await db.SaveChangesAsync();
        return old == budget.TargetCalories ? null : $"Ngân sách cập nhật từ {old} lên {budget.TargetCalories} kcal/ngày theo hồ sơ mới. {budget.Explanation}";
    }
}
