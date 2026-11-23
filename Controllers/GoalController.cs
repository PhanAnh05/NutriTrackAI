using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Filters;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

[Authorize]
[RequireOnboarding]
public class GoalController : Controller
{
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _calculator;
    private readonly ILogger<GoalController> _logger;
    private readonly GoalPlanningService _planner;
    private readonly CalorieBudgetService _budgets;
    private readonly AdaptiveCalorieService _adaptive;
    private readonly IDataProtector _bodyAdjustmentProtector;

    public GoalController(
        AppDbContext context,
        INutritionCalculatorService calculator,
        ILogger<GoalController> logger, GoalPlanningService planner, CalorieBudgetService budgets, AdaptiveCalorieService adaptive,
        IDataProtectionProvider dataProtection)
    {
        _context = context;
        _calculator = calculator;
        _logger = logger;
        _planner = planner; _budgets = budgets; _adaptive = adaptive;
        _bodyAdjustmentProtector = dataProtection.CreateProtector("NutriTrackAI.Goal.BodyAdjustment");
    }

    [HttpGet("/Goals")]
    public IActionResult GoalsAlias() => RedirectToAction(nameof(Index));

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Chống lỗ hổng IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userId = GetCurrentUserId();

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        var activeGoal = await _context.Goals
            .Where(g => g.UserId == userId && g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        var pastGoals = await _context.Goals
            .Where(g => g.UserId == userId && !g.IsActive)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        var bmiInfo = _calculator.GetBmiInfo(profile.CurrentWeightKg, profile.HeightCm, _calculator.CalculateAge(profile.DateOfBirth));

        var budget = await _budgets.GetAsync(userId);
        var model = new GoalIndexViewModel
        {
            Budget = budget, Review = await _adaptive.ReviewAsync(userId),
            ActiveGoal = activeGoal,
            PastGoals = pastGoals,
            CurrentWeightKg = profile.CurrentWeightKg,
            HeightCm = profile.HeightCm,
            Bmi = bmiInfo.Bmi,
            BmiCategory = bmiInfo.Category,
            BmiBadgeClass = bmiInfo.BadgeClass
        };

        if (activeGoal != null)
        {
            model.DailyCalorieTarget = budget.TargetCalories;
            model.TotalWeightChangePlanned = Math.Abs(activeGoal.TargetWeight - activeGoal.StartWeight);
            model.WeightChangeRemainingKg = Math.Abs(profile.CurrentWeightKg - activeGoal.TargetWeight);
            model.DaysRemaining = Math.Max(0, (int)(activeGoal.TargetDate.Date - GoalPolicy.Today).TotalDays);

            if (model.TotalWeightChangePlanned > 0)
            {
                if (activeGoal.GoalType == GoalType.Lose)
                {
                    var lost = activeGoal.StartWeight - profile.CurrentWeightKg;
                    model.TotalWeightChangeAchieved = Math.Max(0, lost);
                    model.ProgressPercent = (int)Math.Clamp((lost / model.TotalWeightChangePlanned) * 100m, 0, 100);
                }
                else if (activeGoal.GoalType == GoalType.Gain)
                {
                    var gained = profile.CurrentWeightKg - activeGoal.StartWeight;
                    model.TotalWeightChangeAchieved = Math.Max(0, gained);
                    model.ProgressPercent = (int)Math.Clamp((gained / model.TotalWeightChangePlanned) * 100m, 0, 100);
                }
                else
                {
                    model.ProgressPercent = 100;
                }
            }
            else
            {
                model.ProgressPercent = 100;
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == GetCurrentUserId());
        if (profile == null) return RedirectToAction("Index", "Onboarding");
        var type = profile.TargetWeightKg < profile.CurrentWeightKg ? GoalType.Lose : profile.TargetWeightKg > profile.CurrentWeightKg ? GoalType.Gain : GoalType.Maintain;
        var analysis = _planner.Analyze(profile, type, profile.TargetWeightKg, 16);
        var minimumWeeks = type == GoalType.Maintain ? 1 : GoalPolicy.MinGoalWeeks;
        var model = new GoalCreateViewModel { GoalType = type, TargetWeight = type == GoalType.Maintain ? null : profile.TargetWeightKg,
            DurationWeeks = Math.Clamp(analysis.RecommendedWeeks, minimumWeeks, 104) };
        Populate(model, profile);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(GoalCreateViewModel model)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == GetCurrentUserId());
        if (profile == null) return BadRequest();
        if (model.GoalType == GoalType.Maintain) { model.TargetWeight = profile.CurrentWeightKg; ModelState.Remove(nameof(model.TargetWeight)); }
        var analysis = _planner.Analyze(profile, model.GoalType, model.TargetWeight, model.DurationWeeks, model.Introductory);
        if (!ModelState.IsValid && !analysis.HideEnergyDetails)
        {
            GoalPlanningService.Block(analysis, "Dữ liệu nhập chưa hợp lệ. Vui lòng kiểm tra cân nặng và số tuần.", true);
            if (ModelState[nameof(model.TargetWeight)]?.Errors.Count > 0 || ModelState[nameof(model.GoalType)]?.Errors.Count > 0)
            {
                analysis.PaceOptions = [];
                analysis.MaxWeeklyRateKg = 0;
                analysis.PaceZone = PaceZone.None;
                analysis.HideEnergyDetails = true;
            }
        }
        if (Request.GetTypedHeaders().Accept?.Any(media => media.MediaType.Value == "application/json") == true)
            return Json(new
            {
                analysis.IsBlocked, analysis.PaceZone, analysis.PaceZoneLabel, analysis.PaceOptions,
                analysis.MaxWeeklyRateKg, analysis.RequiresIntensePaceAck, analysis.MinimumMathematicalWeeks,
                DailyCalorieTarget = analysis.HideEnergyDetails ? (int?)null : analysis.DailyCalorieTarget,
                analysis.HideEnergyDetails, analysis.RequiresHealthScreening, analysis.WarningMessage, analysis.IntroductoryDailyBudgetKcal,
                HealthTier = analysis.Health?.Tier,
                HealthReasons = analysis.Health?.Reasons ?? []
            });
        return PartialView("_EnergyAnalysis", analysis);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GoalCreateViewModel model)
    {
        var userId = GetCurrentUserId();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");
        if (model.GoalType == GoalType.Maintain) { model.TargetWeight = profile.CurrentWeightKg; ModelState.Remove(nameof(model.TargetWeight)); }
        Populate(model, profile);
        if (model.Analysis!.IsBlocked) ModelState.AddModelError("", model.Analysis.WarningMessage!);
        else if (model.Analysis.RequiresIntensePaceAck && !model.AcknowledgeIntensePace)
            ModelState.AddModelError(nameof(model.AcknowledgeIntensePace), GoalPolicy.IntensePaceAcknowledgmentMessage);
        if (!ModelState.IsValid) return View(model);
        var oldGoals = await _context.Goals.Where(g => g.UserId == userId && g.IsActive).ToListAsync();
        foreach (var old in oldGoals) old.IsActive = false;
        // Save deactivation before insert so this also works with a future filtered unique index.
        await _context.SaveChangesAsync();
        var analysis = model.Analysis;
        var state = new CaloriePlanState { Adjustment = analysis.PlannedDailyAdjustment,
            Introductory = model.Introductory && model.GoalType != GoalType.Maintain, LastReviewedOn = GoalPolicy.Today };
        var target = model.TargetWeight!.Value;
        _context.Goals.Add(new Goal { UserId = userId, GoalType = model.GoalType,
            StartWeight = profile.CurrentWeightKg, TargetWeight = target,
            StartDate = GoalPolicy.Today, TargetDate = GoalPolicy.Today.AddDays(model.DurationWeeks * 7),
            WeeklyRateKg = Math.Round(analysis.AppliedWeeklyRateKg, 2, MidpointRounding.AwayFromZero),
            DailyCalorieTarget = analysis.DailyCalorieTarget, IsActive = true,
            WarningFlag = analysis.WarningFlag, WarningMessage = analysis.WarningMessage,
            CaloriePlanJson = state.ToJson() });
        profile.TargetWeightKg = target;
        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    private void Populate(GoalCreateViewModel model, UserProfile profile)
    {
        model.StartWeight = profile.CurrentWeightKg;
        model.HeightCm = profile.HeightCm;
        model.Analysis = _planner.Analyze(profile, model.GoalType, model.TargetWeight, model.DurationWeeks, model.Introductory);
        model.CurrentBmi = model.Analysis.CurrentBmi;
        model.TargetBmi = model.Analysis.TargetBmi;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BodyCheckIn(int id, BodyFeedback? feedback)
    {
        if (!ModelState.IsValid || feedback is not (BodyFeedback.Keep or BodyFeedback.Increase or BodyFeedback.Decrease))
            return BadRequest();

        var userId = GetCurrentUserId();
        if (feedback == BodyFeedback.Keep)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var activeGoal = await FindActiveLossGoal(id, userId);
            if (activeGoal == null) return NotFound();
            var state = _budgets.StateFor(activeGoal);
            state.LastBodyCheckInOn = GoalPolicy.Today;
            activeGoal.CaloriePlanJson = state.ToJson();
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = "Đã ghi nhận phản hồi và giữ nguyên kế hoạch.";
            return RedirectToAction(nameof(Index));
        }

        var goal = await FindActiveLossGoal(id, userId);
        if (goal == null) return NotFound();
        var profile = await _context.UserProfiles.SingleAsync(p => p.UserId == userId);
        var preview = _planner.PreviewBodyAdjustment(profile, goal, _budgets.Calculate(profile, goal),
            feedback.Value, GoalPolicy.Today, out var reason);
        if (preview != null)
            return View("BodyAdjustment", preview with { Fingerprint = _bodyAdjustmentProtector.Protect(preview.Fingerprint) });
        TempData["ErrorMessage"] = reason;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmBodyAdjustment(int id, BodyFeedback? feedback, string fingerprint)
    {
        if (!ModelState.IsValid || feedback is not (BodyFeedback.Increase or BodyFeedback.Decrease)
            || string.IsNullOrEmpty(fingerprint))
            return BadRequest();

        string reviewedFingerprint;
        try { reviewedFingerprint = _bodyAdjustmentProtector.Unprotect(fingerprint); }
        catch (CryptographicException) { return BadRequest(); }

        var userId = GetCurrentUserId();
        var today = GoalPolicy.Today;
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var goal = await FindActiveLossGoal(id, userId);
        if (goal == null) return NotFound();
        var profile = await _context.UserProfiles.SingleAsync(p => p.UserId == userId);
        var preview = _planner.PreviewBodyAdjustment(profile, goal, _budgets.Calculate(profile, goal),
            feedback.Value, today, out var reason);
        if (preview == null || preview.Fingerprint != reviewedFingerprint)
        {
            TempData["ErrorMessage"] = reason ?? "Kế hoạch hoặc hồ sơ đã thay đổi. Hãy xem lại bản dự kiến mới trước khi xác nhận.";
            return RedirectToAction(nameof(Index));
        }

        var state = _budgets.StateFor(goal);
        state.Changes.Add(new(today, preview.Adjustment, "Điều chỉnh theo phản hồi cảm nhận.", state.Adjustment));
        state.Adjustment = preview.Adjustment;
        state.Introductory = false;
        state.LastBodyCheckInOn = today;
        state.Reason = "Đã xác nhận điều chỉnh theo phản hồi cảm nhận.";
        goal.CaloriePlanJson = state.ToJson();
        goal.DailyCalorieTarget = preview.NewCalories;
        goal.WeeklyRateKg = Math.Round(preview.NewWeeklyRate, 2, MidpointRounding.AwayFromZero);
        goal.TargetDate = preview.NewTargetDate ?? goal.TargetDate;
        goal.WarningFlag = state.Adjustment > GoalPolicy.UsualDeficit;
        goal.WarningMessage = goal.WarningFlag ? "Mức điều chỉnh cao hơn mức khởi tạo. Hãy tiếp tục theo dõi cảm nhận." : null;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["SuccessMessage"] = "Đã cập nhật tốc độ và ngân sách calo.";
        return RedirectToAction(nameof(Index));
    }

    private Task<Goal?> FindActiveLossGoal(int id, int userId) =>
        _context.Goals.SingleOrDefaultAsync(g => g.Id == id && g.UserId == userId && g.IsActive && g.GoalType == GoalType.Lose);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, string fingerprint, bool apply, bool confirmed, decimal? adjustment)
    {
        var userId = GetCurrentUserId();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var goal = await _context.Goals.SingleOrDefaultAsync(g => g.Id == id && g.UserId == userId && g.IsActive);
        if (goal == null) return NotFound();
        var review = await _adaptive.ReviewAsync(userId);
        if (!review.IsDue || review.Fingerprint != fingerprint || (apply && !confirmed))
        {
            TempData["ErrorMessage"] = "Dữ liệu đã thay đổi hoặc thiếu xác nhận. Hãy xem lại đề xuất mới.";
            return RedirectToAction(nameof(Index));
        }
        var state = _budgets.StateFor(goal);
        if (apply)
        {
            var requested = adjustment ?? review.ProposedAdjustment;
            if (review.ProposedAdjustment == null || requested == null ||
                (requested != review.ProposedAdjustment && !(review.MayIncrease && requested > state.Adjustment &&
                    requested <= (goal.GoalType == GoalType.Lose ? GoalPolicy.MaxDeficitHighBmi : GoalPolicy.MaximumSurplus))))
                return BadRequest("Mức điều chỉnh không thuộc đề xuất hiện tại.");
            var profile = await _context.UserProfiles.SingleAsync(p => p.UserId == userId);
            var type = review.GoalReached ? GoalType.Maintain : goal.GoalType;
            if (!_planner.CanApply(profile, type, goal.TargetWeight, requested.Value, out var reason, goal.StartWeight, allowTolerance: false))
            {
                TempData["ErrorMessage"] = reason;
                return RedirectToAction(nameof(Index));
            }
            state.Changes.Add(new(GoalPolicy.Today, requested.Value, review.Message, state.Adjustment));
            state.Adjustment = requested.Value;
            state.Introductory = false;
            state.Reason = review.GoalReached ? "Đã xác nhận chuyển sang duy trì sau khi đạt đích." : "Đã xác nhận điều chỉnh sau khi xem đánh giá xu hướng và nhật ký.";
            goal.GoalType = type;
            if (type == GoalType.Maintain) profile.TargetWeightKg = goal.TargetWeight = profile.CurrentWeightKg;
            goal.WeeklyRateKg = Math.Round(state.Adjustment * 7m / GoalPolicy.EnergyPerKg, 2);
            goal.WarningFlag = state.Adjustment > GoalPolicy.UsualDeficit;
            goal.WarningMessage = goal.WarningFlag ? "Mức điều chỉnh trên khoảng thông thường đã được xác nhận; cần tiếp tục theo dõi và đánh giá định kỳ." : null;
        }
        else state.Reason = "Người dùng đã xem đánh giá và chọn giữ kế hoạch hiện tại.";
        state.LastReviewedOn = GoalPolicy.Today;
        goal.CaloriePlanJson = state.ToJson();
        try
        {
            await _context.SaveChangesAsync();
            await _budgets.RefreshAsync(userId);
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = apply ? "Đã áp dụng kế hoạch bạn xác nhận." : "Đã giữ kế hoạch hiện tại. Đánh giá tiếp sau 14 ngày.";
        }
        catch (DbUpdateConcurrencyException) { TempData["ErrorMessage"] = "Kế hoạch vừa thay đổi. Hãy tải lại và kiểm tra trước khi xác nhận."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Extend(int id, int weeks)
    {
        var goal = await _context.Goals.SingleOrDefaultAsync(g => g.Id == id && g.UserId == GetCurrentUserId() && g.IsActive);
        if (goal == null) return NotFound();
        if (weeks is < 1 or > 104) return BadRequest();
        var date = GoalPolicy.Today.AddDays(weeks * 7);
        if (date <= goal.TargetDate) return BadRequest("Thời gian mới phải dài hơn ngày mong muốn hiện tại.");
        goal.TargetDate = date;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Đã cập nhật ngày mong muốn theo xác nhận; giữ mức điều chỉnh năng lượng.";
        return RedirectToAction(nameof(Index));
    }

    private int GetCurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
