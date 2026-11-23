using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _calculator;
    private readonly ILogger<OnboardingController> _logger;
    private readonly GoalPlanningService _planner;
    private readonly CalorieBudgetService _budgets;
    private readonly IWeightTrackingService _weights;

    public OnboardingController(
        AppDbContext context,
        INutritionCalculatorService calculator,
        ILogger<OnboardingController> logger, GoalPlanningService planner, CalorieBudgetService budgets,
        IWeightTrackingService weights)
    {
        _context = context;
        _calculator = calculator;
        _logger = logger;
        _planner = planner;
        _budgets = budgets;
        _weights = weights;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        var model = new OnboardingViewModel();
        if (profile != null)
        {
            model.Gender = profile.Gender;
            model.DateOfBirth = profile.DateOfBirth != default ? profile.DateOfBirth : GoalPolicy.Today.AddYears(-22);
            if (profile.HeightCm > 0) model.HeightCm = profile.HeightCm;
            if (profile.CurrentWeightKg > 0) model.CurrentWeightKg = profile.CurrentWeightKg;
            if (profile.TargetWeightKg >= 20 && profile.TargetWeightKg != profile.CurrentWeightKg)
            {
                model.TargetWeightKg = profile.TargetWeightKg;
                model.GoalType = profile.TargetWeightKg < profile.CurrentWeightKg ? GoalType.Lose : GoalType.Gain;
            }
            model.ActivityLevel = profile.ActivityLevel;
            model.DietType = profile.DietType;
            model.Allergies = profile.Allergies;
            model.DislikedFoods = profile.DislikedFoods;
            model.HealthNotes = profile.HealthNotes;
            model.Screening = HealthScreeningForm.From(profile);
            if (profile.DailyWaterGoalMl > 0) model.DailyWaterGoalMl = profile.DailyWaterGoalMl;
        }

        // Tính toán ban đầu
        PopulatePreview(model, profile);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculatePreview([FromBody] OnboardingViewModel input)
    {
        if (input == null)
        {
            return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var profile = await _context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);
        var trackingOnly = PopulatePreview(input, profile);
        var analysis = input.Analysis!;

        return Json(new
        {
            success = true,
            age = input.Age,
            bmi = input.Bmi,
            bmiCategory = input.BmiCategory,
            bmiBadgeClass = input.BmiBadgeClass,
            bmr = analysis.HideEnergyDetails ? (decimal?)null : input.Bmr,
            tdee = analysis.HideEnergyDetails ? (decimal?)null : input.Tdee,
            dailyCalorieTarget = analysis.HideEnergyDetails ? (int?)null : input.DailyCalorieTarget,
            weeklyRateKg = analysis.HideEnergyDetails ? (decimal?)null : input.WeeklyRateKg,
            analysis.HideEnergyDetails,
            analysis.RequiresHealthScreening,
            analysis.PaceOptions,
            analysis.MaxWeeklyRateKg,
            analysis.PaceZone,
            analysis.PaceZoneLabel,
            analysis.RequiresIntensePaceAck,
            analysis.AppliedWeeklyRateKg,
            analysis.PlannedDailyAdjustment,
            analysis.IntroductoryDailyBudgetKcal,
            analysis.MinimumMathematicalWeeks,
            healthTier = analysis.Health?.Tier,
            healthReasons = analysis.Health?.Reasons ?? [],
            recommendIntroductory = !trackingOnly && analysis.Health?.Tier == HealthTier.NeedsDoctor && input.GoalType != GoalType.Maintain,
            safetyLevel = (int)input.SafetyLevel,
            isBlocked = input.IsBlocked,
            trackingOnly,
            blockedReason = input.BlockedReason,
            recommendedWeeks = input.RecommendedWeeks,
            minRecommendedWeeks = input.MinRecommendedWeeks,
            maxRecommendedWeeks = input.MaxRecommendedWeeks,
            currentBmi = input.CurrentBmi,
            targetBmi = input.TargetBmi,
            minSafeWeight = input.MinSafeWeight,
            maxNormalWeight = input.MaxNormalWeight,
            maxAllowedWeight = input.MaxAllowedWeight,
            suggestedSafeWeight = input.SuggestedSafeWeight,
            warningFlag = input.WarningFlag,
            warningMessage = input.WarningMessage,
            proteinGrams = input.ProteinGrams,
            carbGrams = input.CarbGrams,
            fatGrams = input.FatGrams,
            proteinPercent = input.ProteinPercent,
            carbPercent = input.CarbPercent,
            fatPercent = input.FatPercent,
            input.MacroUnavailableReason
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(OnboardingViewModel model)
    {
        // Chống lỗ hổng IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var previouslyRestricted = profile != null
            && GoalPlanningService.ScopeRestriction(profile, _calculator.CalculateAge(profile.DateOfBirth)) != null;
        var planningProfile = BuildPlanningProfile(model, profile);
        if (planningProfile.HealthScreeningAt == null)
            ModelState.AddModelError(nameof(model.Screening), GoalPolicy.CompleteHealthScreening);
        var trackingOnly = GoalPlanningService.ScopeRestriction(planningProfile, _calculator.CalculateAge(model.DateOfBirth)) != null
            || (profile?.OnboardingCompleted == true && previouslyRestricted);

        if (model.GoalType == GoalType.Maintain || trackingOnly)
        {
            model.TargetWeightKg = model.CurrentWeightKg;
            ModelState.Remove(nameof(model.TargetWeightKg));
        }
        if (trackingOnly) ModelState.Remove(nameof(model.DurationWeeks));
        // Backend kiểm tra lại dữ liệu và điều kiện áp dụng.
        var calorieResult = _planner.Analyze(planningProfile, model.GoalType, model.TargetWeightKg, model.DurationWeeks, model.Introductory);

        if (calorieResult.IsBlocked && !trackingOnly)
        {
            ModelState.AddModelError("", calorieResult.BlockedReason ?? "Mục tiêu chưa đáp ứng quy tắc hệ thống.");
        }
        else if (!trackingOnly && calorieResult.RequiresIntensePaceAck && !model.AcknowledgeIntensePace)
        {
            ModelState.AddModelError(nameof(model.AcknowledgeIntensePace), GoalPolicy.IntensePaceAcknowledgmentMessage);
        }

        if (!ModelState.IsValid)
        {
            PopulatePreview(model, profile);
            return View("Index", model);
        }

        // 1. Cập nhật UserProfile
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }

        profile.Gender = model.Gender;
        profile.DateOfBirth = model.DateOfBirth;
        profile.HeightCm = model.HeightCm;
        profile.CurrentWeightKg = model.CurrentWeightKg;
        profile.TargetWeightKg = model.TargetWeightKg!.Value;
        profile.ActivityLevel = model.ActivityLevel;
        profile.DietType = model.DietType;
        profile.Allergies = model.Allergies?.Trim();
        profile.DislikedFoods = model.DislikedFoods?.Trim();
        profile.HealthNotes = model.HealthNotes?.Trim();
        profile.MedicalConditions = planningProfile.MedicalConditions;
        profile.DoctorAwareOfPlan = planningProfile.DoctorAwareOfPlan;
        profile.HealthScreeningAt = planningProfile.HealthScreeningAt;
        profile.DailyWaterGoalMl = model.DailyWaterGoalMl > 0 ? model.DailyWaterGoalMl : 2000;
        profile.OnboardingCompleted = true;
        profile.UpdatedAt = DateTime.UtcNow;

        // Giữ mục tiêu cũ khi chuyển sang chỉ theo dõi.
        var activeGoals = await _context.Goals.Where(g => g.UserId == userId && g.IsActive).ToListAsync();
        foreach (var oldGoal in activeGoals)
        {
            if (trackingOnly) _budgets.ReevaluateAfterProfileUpdate(profile, oldGoal, previouslyRestricted);
            else oldGoal.IsActive = false;
        }

        await _context.SaveChangesAsync();

        if (!trackingOnly)
        {
            _context.Goals.Add(new Goal
            {
                UserId = userId,
                GoalType = model.GoalType,
                StartWeight = model.CurrentWeightKg,
                TargetWeight = profile.TargetWeightKg,
                StartDate = GoalPolicy.Today,
                TargetDate = GoalPolicy.Today.AddDays(model.DurationWeeks * 7),
                WeeklyRateKg = Math.Round(calorieResult.AppliedWeeklyRateKg, 2, MidpointRounding.AwayFromZero),
                CaloriePlanJson = new CaloriePlanState { Adjustment = calorieResult.PlannedDailyAdjustment,
                    Introductory = model.Introductory && model.GoalType != GoalType.Maintain, LastReviewedOn = GoalPolicy.Today }.ToJson(),
                DailyCalorieTarget = calorieResult.DailyCalorieTarget,
                IsActive = true,
                WarningFlag = calorieResult.WarningFlag,
                WarningMessage = calorieResult.WarningMessage,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (activeGoals.Count > 0)
        {
            profile.TargetWeightKg = activeGoals.OrderByDescending(g => g.CreatedAt).First().TargetWeight;
        }

        await _weights.RecordWeightAsync(profile, GoalPolicy.Today, model.CurrentWeightKg,
            "Khởi tạo cân nặng ban đầu từ khảo sát Onboarding");
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        _logger.LogInformation("Người dùng {UserId} đã hoàn tất Onboarding thành công.", userId);

        return RedirectToAction("Index", "Dashboard");
    }

    private bool PopulatePreview(OnboardingViewModel model, UserProfile? profile)
    {
        if (model.GoalType == GoalType.Maintain) model.TargetWeightKg = model.CurrentWeightKg;
        model.Age = _calculator.CalculateAge(model.DateOfBirth);
        var bmiInfo = _calculator.GetBmiInfo(model.CurrentWeightKg, model.HeightCm, model.Age);
        model.Bmi = bmiInfo.Bmi;
        model.BmiCategory = bmiInfo.Category;
        model.BmiBadgeClass = bmiInfo.BadgeClass;

        model.Bmr = _calculator.CalculateBmr(model.Gender, model.CurrentWeightKg, model.HeightCm, model.Age);
        model.Tdee = _calculator.CalculateTdee(model.Bmr, model.ActivityLevel);

        var planningProfile = BuildPlanningProfile(model, profile);
        var screened = planningProfile.HealthScreeningAt.HasValue;
        var mustUpdateScreening = profile is { OnboardingCompleted: true } &&
            GoalPlanningService.ScopeRestriction(profile, _calculator.CalculateAge(profile.DateOfBirth)) != null;
        var trackingOnly = screened && (mustUpdateScreening || GoalPlanningService.ScopeRestriction(planningProfile, model.Age) != null);
        var calorieResult = _planner.Analyze(planningProfile, model.GoalType, model.TargetWeightKg, model.DurationWeeks, model.Introductory);
        if (!screened || (mustUpdateScreening && !calorieResult.RequiresHealthScreening))
        {
            GoalPlanningService.Block(calorieResult, GoalPolicy.CompleteHealthScreening);
            calorieResult.HideEnergyDetails = true;
            calorieResult.RequiresHealthScreening = true;
            calorieResult.PaceOptions = [];
            calorieResult.PaceZone = PaceZone.None;
            calorieResult.DailyCalorieTarget = 0;
            calorieResult.IntroductoryDailyBudgetKcal = null;
            calorieResult.MinimumMathematicalWeeks = calorieResult.RecommendedWeeks = calorieResult.MinRecommendedWeeks = calorieResult.MaxRecommendedWeeks = 0;
            calorieResult.PlannedDailyAdjustment = 0;
        }

        model.Analysis = calorieResult;
        model.DailyCalorieTarget = calorieResult.DailyCalorieTarget;
        model.WeeklyRateKg = calorieResult.WeeklyRateKg;
        model.SafetyLevel = calorieResult.SafetyLevel;
        model.IsBlocked = calorieResult.IsBlocked;
        model.BlockedReason = calorieResult.BlockedReason;
        model.RecommendedWeeks = calorieResult.RecommendedWeeks;
        model.MinRecommendedWeeks = calorieResult.MinRecommendedWeeks;
        model.MaxRecommendedWeeks = calorieResult.MaxRecommendedWeeks;
        model.CurrentBmi = calorieResult.CurrentBmi;
        model.TargetBmi = calorieResult.TargetBmi;
        model.MinSafeWeight = calorieResult.MinSafeWeight;
        model.MaxNormalWeight = calorieResult.MaxNormalWeight;
        model.MaxAllowedWeight = calorieResult.MaxAllowedWeight;
        model.SuggestedSafeWeight = calorieResult.SuggestedSafeWeight;
        model.WarningFlag = calorieResult.WarningFlag;
        model.WarningMessage = calorieResult.WarningMessage;

        var macros = _calculator.CalculateRecommendedMacros(
            model.DailyCalorieTarget,
            model.DietType,
            model.GoalType,
            model.CurrentWeightKg,
            calorieResult.Health?.Constraints);

        model.ProteinGrams = macros.ProteinGrams;
        model.CarbGrams = macros.CarbGrams;
        model.FatGrams = macros.FatGrams;
        model.ProteinPercent = macros.ProteinPercent;
        model.CarbPercent = macros.CarbPercent;
        model.FatPercent = macros.FatPercent;
        model.MacroUnavailableReason = macros.UnavailableReason;
        return trackingOnly;
    }

    private static UserProfile BuildPlanningProfile(OnboardingViewModel model, UserProfile? persisted)
    {
        var profile = new UserProfile
        {
            DateOfBirth = model.DateOfBirth, Gender = model.Gender, HeightCm = model.HeightCm,
            CurrentWeightKg = model.CurrentWeightKg, ActivityLevel = model.ActivityLevel,
            HealthNotes = model.HealthNotes,
            MedicalConditions = persisted?.MedicalConditions ?? MedicalConditions.None,
            DoctorAwareOfPlan = persisted?.DoctorAwareOfPlan ?? false,
            HealthScreeningAt = persisted?.HealthScreeningAt,
            HealthScreeningJson = persisted?.HealthScreeningJson
        };
        model.Screening?.ApplyTo(profile);
        return profile;
    }
}
