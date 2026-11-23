using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public class PlanReview
{
    public bool IsDue { get; set; }
    public bool SufficientData { get; set; }
    public int WeightDays { get; set; }
    public int MealDays { get; set; }
    public decimal? WeeklyRate { get; set; }
    public decimal? FirstWindowAverage { get; set; }
    public decimal? LastWindowAverage { get; set; }
    public decimal? TrendIntervalDays { get; set; }
    public decimal? PeriodFirstWeight { get; set; }
    public decimal? PeriodLastWeight { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public decimal? EndpointWeeklyRate { get; set; }
    public decimal? ProposedAdjustment { get; set; }
    public bool MayIncrease { get; set; }
    public bool GoalReached { get; set; }
    public string Message { get; set; } = "Chưa đủ dữ liệu đánh giá.";
    public string Fingerprint { get; set; } = "";
}

public record MealDay(DateTime Date, decimal Calories, decimal ExerciseCalories);

public class AdaptiveCalorieService(AppDbContext db, CalorieBudgetService budgets, GoalPlanningService planner)
{
    public async Task<PlanReview> ReviewAsync(int userId)
    {
        var budget = await budgets.GetAsync(userId);
        if (budget.Goal == null) return new();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        var today = GoalPolicy.Today;
        var since = today.AddDays(-27);
        if (since < budget.Goal.StartDate) since = budget.Goal.StartDate;
        var weights = await db.WeightLogs.Where(w => w.UserId == userId && w.LoggedOn >= since && w.LoggedOn < today.AddDays(1)).ToListAsync();
        var meals = await db.MealLogs.Include(m => m.Details).Where(m => m.UserId == userId && m.LoggedOn >= since && m.LoggedOn < today.AddDays(1)).ToListAsync();
        var exercises = await db.ExerciseLogs.Where(e => e.UserId == userId && e.LoggedOn >= since && e.LoggedOn < today.AddDays(1)).ToListAsync();
        var mealDays = meals.GroupBy(m => m.LoggedOn.Date).Select(g => new MealDay(g.Key,
            g.Sum(m => m.Details.Sum(d => d.Calories)), exercises.Where(e => e.LoggedOn.Date == g.Key).Sum(e => e.CaloriesBurned))).ToList();
        var result = Evaluate(budget, budgets.StateFor(budget.Goal), weights, mealDays, today);
        if (result.ProposedAdjustment.HasValue && !planner.CanApply(profile,
            result.GoalReached ? GoalType.Maintain : budget.Goal.GoalType, budget.Goal.TargetWeight,
            result.ProposedAdjustment.Value, out var reason, budget.Goal.StartWeight, allowTolerance: false))
        {
            result.ProposedAdjustment = null;
            result.MayIncrease = false;
            result.Message = reason ?? "Hãy kéo dài thời gian và xin hỗ trợ chuyên môn.";
        }
        var material = JsonSerializer.Serialize(new { budget.Goal.Id, budget.Goal.CaloriePlanJson, budget.Tdee,
            profile.CurrentWeightKg, profile.HeightCm, profile.Gender, profile.DateOfBirth, profile.MedicalConditions,
            profile.DoctorAwareOfPlan, profile.HealthScreeningAt, budget.Health.Tier, budget.MaximumAdjustment,
            budget.Goal.TargetDate, budget.Goal.TargetWeight, budget.Goal.GoalType, budget.Goal.StartDate, weights = weights.OrderBy(w => w.Id).Select(w => new { w.Id, w.LoggedOn, w.WeightKg }),
            mealDays = mealDays.OrderBy(m => m.Date), result.ProposedAdjustment });
        result.Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return result;
    }

    public static PlanReview Evaluate(CalorieBudget budget, CaloriePlanState state,
        IEnumerable<WeightLog> logs, IReadOnlyList<MealDay> meals, DateTime today)
    {
        var result = new PlanReview { IsDue = today.Date >= state.LastReviewedOn.Date.AddDays(GoalPolicy.ReviewDays) };
        if (budget.Goal == null || budget.Suspended) { result.Message = budget.Explanation; return result; }
        var goal = budget.Goal;
        var daily = logs.Where(w => w.LoggedOn.Date >= goal.StartDate.Date && w.LoggedOn.Date <= today.Date)
            .GroupBy(w => w.LoggedOn.Date).Select(g => (Date: g.Key, Weight: g.Average(w => w.WeightKg))).OrderBy(w => w.Date).ToList();
        var recent = daily.Where(w => w.Date >= today.AddDays(-13)).ToList();
        var first = recent.Where(w => w.Date < today.AddDays(-6)).ToList();
        var last = recent.Where(w => w.Date >= today.AddDays(-6)).ToList();
        result.WeightDays = recent.Count;
        result.MealDays = meals.Count(m => m.Date >= today.AddDays(-13) && m.Calories > 0);
        result.SufficientData = first.Count >= 2 && last.Count >= 2 && (recent.Last().Date - recent.First().Date).Days >= 10;
        if (!result.IsDue) { result.Message = "Chưa đến chu kỳ đánh giá 14 ngày. Giữ mức điều chỉnh đã xác nhận."; return result; }
        if (!result.SufficientData) { result.Message = "Chưa đủ dữ liệu: cần ít nhất 2 ngày cân mỗi nửa chu kỳ và trải dài ít nhất 10 ngày. Không kết luận chững cân; hãy ghi nhận thêm."; return result; }
        var elapsed = (decimal)(last.Average(w => (decimal)(w.Date - today.Date).Ticks) - first.Average(w => (decimal)(w.Date - today.Date).Ticks)) / TimeSpan.TicksPerDay;
        var signedChange = (last.Average(w => w.Weight) - first.Average(w => w.Weight)) * 7m / elapsed;
        var actual = goal.GoalType == GoalType.Lose ? -signedChange : signedChange;
        result.WeeklyRate = actual;
        result.FirstWindowAverage = first.Average(w => w.Weight);
        result.LastWindowAverage = last.Average(w => w.Weight);
        result.TrendIntervalDays = elapsed;
        result.PeriodFirstWeight = recent.First().Weight;
        result.PeriodLastWeight = recent.Last().Weight;
        result.PeriodStart = recent.First().Date;
        result.PeriodEnd = recent.Last().Date;
        var periodDays = (result.PeriodEnd.Value - result.PeriodStart.Value).Days;
        result.EndpointWeeklyRate = (result.PeriodLastWeight - result.PeriodFirstWeight) * 7m / periodDays
            * (goal.GoalType == GoalType.Lose ? -1 : 1);
        var latestAverage = last.Average(w => w.Weight);
        result.GoalReached = goal.GoalType == GoalType.Lose ? latestAverage <= goal.TargetWeight : goal.GoalType == GoalType.Gain && latestAverage >= goal.TargetWeight;
        if (result.GoalReached) { result.ProposedAdjustment = 0; result.Message = "Xu hướng cân nặng đã đạt đích. Bạn có thể xác nhận chuyển sang duy trì theo TDEE mới."; return result; }
        if (goal.GoalType == GoalType.Maintain) { result.Message = "Đang duy trì: theo dõi xu hướng; không tự thay đổi calo theo dao động một ngày."; return result; }
        var maxRate = GoalPolicy.MaximumWeeklyRate(goal.GoalType, latestAverage);
        if (actual > maxRate)
        {
            result.ProposedAdjustment = Math.Min(budget.MaximumAdjustment, Math.Max(0, state.Adjustment - 150m));
            result.Message = "Xu hướng thay đổi nhanh hơn mức tham chiếu. Đề xuất giảm mức điều chỉnh 150 kcal/ngày và cân nhắc hỗ trợ chuyên môn.";
            return result;
        }
        var expected = state.Adjustment * 7m / GoalPolicy.EnergyPerKg;
        var earlier = daily.Where(w => w.Date >= today.AddDays(-27) && w.Date < today.AddDays(-13)).ToList();
        var earlierFirst = earlier.Where(w => w.Date < today.AddDays(-20)).ToList();
        var earlierLast = earlier.Where(w => w.Date >= today.AddDays(-20)).ToList();
        var earlierEnough = earlierFirst.Count >= 2 && earlierLast.Count >= 2 && (earlier.Last().Date - earlier.First().Date).Days >= 10;
        var earlierDays = earlierEnough ? (decimal)(earlierLast.Average(w => (decimal)(w.Date - today.Date).Ticks) - earlierFirst.Average(w => (decimal)(w.Date - today.Date).Ticks)) / TimeSpan.TicksPerDay : 1;
        var earlierChange = earlierEnough ? (earlierLast.Average(w => w.Weight) - earlierFirst.Average(w => w.Weight)) * 7m / earlierDays * (goal.GoalType == GoalType.Lose ? -1 : 1) : decimal.MaxValue;
        var foodReliable = meals.Count(m => m.Date >= today.AddDays(-27) && m.Date < today.AddDays(-13) && m.Calories > 0) >= 10 && result.MealDays >= 10;
        var recentMeals = meals.Where(m => m.Date >= today.AddDays(-13) && m.Calories > 0).ToList();
        var adherent = recentMeals.Count > 0 && Math.Abs(recentMeals.Average(m => m.Calories - GoalPolicy.ExerciseCredit(m.ExerciseCalories)) - budget.TargetCalories) <= budget.TargetCalories * 0.15m;
        var introductoryLimit = Math.Min(goal.GoalType == GoalType.Lose ? GoalPolicy.UsualDeficit : GoalPolicy.MaximumSurplus,
            budget.MaximumAdjustment);
        if (state.Introductory && result.MealDays >= 10 && adherent && actual >= 0 && actual <= maxRate && state.Adjustment < introductoryLimit)
        {
            result.ProposedAdjustment = Math.Min(introductoryLimit, state.Adjustment + GoalPolicy.IntroAdjustment);
            result.Message = "Dữ liệu giai đoạn làm quen tương đối đầy đủ, xu hướng chưa vượt mức tham chiếu. Bạn có thể xác nhận chuyển sang giai đoạn ổn định hoặc giữ mức hiện tại; hệ thống kiểm tra lại sàn calo trước khi áp dụng.";
            return result;
        }
        var slow = actual < expected * 0.6m && earlierChange < expected * 0.6m;
        if (earlierEnough && foodReliable && adherent && slow && today >= (state.Changes.LastOrDefault()?.On ?? goal.StartDate).AddDays(28))
        {
            if (state.Adjustment < budget.MaximumAdjustment)
            {
                result.MayIncrease = true;
                result.ProposedAdjustment = Math.Min(budget.MaximumAdjustment, state.Adjustment + 100m);
                result.Message = "Hai chu kỳ có xu hướng chậm hơn kế hoạch, nhật ký ăn tương đối đầy đủ. Có thể cân nhắc tăng mức điều chỉnh hoặc kéo dài thời gian; dữ liệu tự ghi không chứng minh chính xác tuân thủ.";
                return result;
            }
        }
        result.Message = state.Introductory ? "Đã đến kỳ đánh giá giai đoạn làm quen. Giữ mức hiện tại; chỉ cân nhắc tăng sau khi có xu hướng chậm kéo dài và đủ nhật ký." : "Giữ mức điều chỉnh hiện tại và cập nhật TDEE. Chưa có đủ căn cứ đề xuất tăng mức điều chỉnh.";
        return result;
    }
}
