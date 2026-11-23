using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
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
public class ProfileController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INutritionCalculatorService _calculator;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<ProfileController> _logger;
    private readonly CalorieBudgetService _budgets;
    private readonly IWeightTrackingService _weights;

    public ProfileController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        INutritionCalculatorService calculator,
        IWebHostEnvironment webHostEnvironment,
        ILogger<ProfileController> logger, CalorieBudgetService budgets, IWeightTrackingService weights)
    {
        _context = context;
        _userManager = userManager;
        _calculator = calculator;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
        _budgets = budgets;
        _weights = weights;
    }

    [HttpGet]
    [RequireOnboarding]
    public async Task<IActionResult> Index()
    {
        // Chống lỗ hổng IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userId = GetCurrentUserId();

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null) return RedirectToAction("Login", "Account");

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        var model = new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            AvatarUrl = profile.AvatarUrl,
            Gender = profile.Gender,
            DateOfBirth = profile.DateOfBirth,
            HeightCm = profile.HeightCm,
            CurrentWeightKg = profile.CurrentWeightKg,
            TargetWeightKg = profile.TargetWeightKg,
            ActivityLevel = profile.ActivityLevel,
            DietType = profile.DietType,
            Allergies = profile.Allergies,
            DislikedFoods = profile.DislikedFoods,
            HealthNotes = profile.HealthNotes,
            Screening = HealthScreeningForm.From(profile),
            DailyWaterGoalMl = profile.DailyWaterGoalMl,
            WeightMismatch = WeightMismatchViewModel.From(profile.CurrentWeightKg, await _weights.GetLatestAsync(userId))
        };

        await PopulateCalculationsAsync(model, userId);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireOnboarding]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        var userId = GetCurrentUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (user == null || profile == null) return RedirectToAction("Login", "Account");

        var today = GoalPolicy.Today;
        var latest = await _weights.GetLatestAsync(userId);
        model.WeightMismatch = WeightMismatchViewModel.From(profile.CurrentWeightKg, latest);
        var weightChanged = !WeightTrackingService.SameWeight(model.CurrentWeightKg, profile.CurrentWeightKg);
        var matchesLog = latest != null && WeightTrackingService.SameWeight(model.CurrentWeightKg, latest.WeightKg);
        if (ModelState.IsValid && weightChanged && !matchesLog && !model.ConfirmLargeWeightChange &&
            WeightTrackingService.LargeChangeWarning(latest, today, model.CurrentWeightKg) is { } warning)
        {
            ModelState.AddModelError(nameof(model.ConfirmLargeWeightChange), warning);
        }

        if (!ModelState.IsValid)
        {
            model.Email = user.Email ?? string.Empty;
            model.AvatarUrl = profile.AvatarUrl;
            await PopulateCalculationsAsync(model, userId);
            return View(model);
        }

        // Xử lý upload ảnh đại diện (<= 5MB, .jpg, .jpeg, .png, .webp)
        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            try
            {
                var avatarPath = await SaveFileAsync(model.AvatarFile, "avatars");
                profile.AvatarUrl = avatarPath;
                model.AvatarUrl = avatarPath;
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(model.AvatarFile), ex.Message);
                model.Email = user.Email ?? string.Empty;
                model.AvatarUrl = profile.AvatarUrl;
                await PopulateCalculationsAsync(model, userId);
                return View(model);
            }
        }

        // Cập nhật thông tin User
        user.FullName = model.FullName.Trim();
        await _userManager.UpdateAsync(user);

        var previouslyRestricted = GoalPlanningService.ScopeRestriction(profile, _calculator.CalculateAge(profile.DateOfBirth)) != null;
        // Cập nhật UserProfile
        profile.Gender = model.Gender;
        profile.DateOfBirth = model.DateOfBirth;
        profile.HeightCm = model.HeightCm;
        var activeGoal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);
        var previousTarget = activeGoal?.DailyCalorieTarget;
        profile.TargetWeightKg = activeGoal?.TargetWeight ?? model.TargetWeightKg;
        model.TargetWeightKg = profile.TargetWeightKg;
        ModelState.Remove(nameof(model.TargetWeightKg));
        profile.ActivityLevel = model.ActivityLevel;
        profile.DietType = model.DietType;
        profile.Allergies = model.Allergies?.Trim();
        profile.DislikedFoods = model.DislikedFoods?.Trim();
        profile.HealthNotes = model.HealthNotes?.Trim();
        var mustReconfirm = model.Screening.ApplyTo(profile);
        if (mustReconfirm)
        {
            model.Screening.DoctorAwareOfPlan = false;
            ModelState.Remove($"{nameof(model.Screening)}.{nameof(HealthScreeningForm.DoctorAwareOfPlan)}");
        }
        profile.DailyWaterGoalMl = model.DailyWaterGoalMl;
        profile.UpdatedAt = DateTime.UtcNow;

        if (weightChanged)
        {
            if (matchesLog)
                _weights.UseLatestWeight(profile, latest!);
            else
                await _weights.RecordWeightAsync(profile, today, model.CurrentWeightKg, "Cập nhật từ Hồ sơ");
        }

        if (activeGoal != null) _budgets.ReevaluateAfterProfileUpdate(profile, activeGoal, previouslyRestricted);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Người dùng {UserId} đã cập nhật hồ sơ sức khỏe thành công.", userId);

        model.Email = user.Email ?? string.Empty;
        model.StatusMessage = "Cập nhật hồ sơ thành công. Bạn có thể xem kế hoạch và ngân sách hiện tại tại trang Mục tiêu.";
        if (mustReconfirm)
            model.StatusMessage += "Danh sách tình trạng cần bác sĩ xác nhận đã thay đổi. Vui lòng xác nhận lại sau khi bác sĩ biết kế hoạch.";
        if (activeGoal != null && previousTarget != activeGoal.DailyCalorieTarget)
            model.StatusMessage += $" Ngân sách cập nhật từ {previousTarget} thành {activeGoal.DailyCalorieTarget} kcal/ngày.";
        model.WeightMismatch = WeightMismatchViewModel.From(profile.CurrentWeightKg, await _weights.GetLatestAsync(userId));
        await PopulateCalculationsAsync(model, userId);
        await transaction.CommitAsync();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Screening(string? returnUrl)
    {
        var profile = await _context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == GetCurrentUserId());
        return View(new HealthScreeningPageViewModel
        {
            Screening = profile == null ? new() : HealthScreeningForm.From(profile),
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Screening(HealthScreeningPageViewModel model)
    {
        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;
        ModelState.Remove(nameof(model.ReturnUrl));
        if (!model.Screening.HasAnswer)
            ModelState.AddModelError(nameof(model.Screening), GoalPolicy.CompleteHealthScreening);
        if (!ModelState.IsValid) return View(model);

        var userId = GetCurrentUserId();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }
        var previouslyRestricted = GoalPlanningService.ScopeRestriction(profile, _calculator.CalculateAge(profile.DateOfBirth)) != null;
        model.Screening.ApplyTo(profile);
        profile.UpdatedAt = DateTime.UtcNow;
        var activeGoal = await _context.Goals.FirstOrDefaultAsync(goal => goal.UserId == userId && goal.IsActive);
        if (activeGoal != null) _budgets.ReevaluateAfterProfileUpdate(profile, activeGoal, previouslyRestricted);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return model.ReturnUrl != null ? LocalRedirect(model.ReturnUrl) : RedirectToAction("Index", "Dashboard");
    }

    private async Task PopulateCalculationsAsync(ProfileViewModel model, int userId)
    {
        model.Age = _calculator.CalculateAge(model.DateOfBirth);
        var bmiInfo = _calculator.GetBmiInfo(model.CurrentWeightKg, model.HeightCm, _calculator.CalculateAge(model.DateOfBirth));
        model.Bmi = bmiInfo.Bmi;
        model.BmiCategory = bmiInfo.Category;
        model.BmiBadgeClass = bmiInfo.BadgeClass;

        model.Bmr = _calculator.CalculateBmr(model.Gender, model.CurrentWeightKg, model.HeightCm, model.Age);
        model.Tdee = _calculator.CalculateTdee(model.Bmr, model.ActivityLevel);

        var budget = await _budgets.GetAsync(userId);
        model.DailyCalorieTarget = budget.TargetCalories;
        model.HasActiveGoal = budget.Goal != null;
        model.PlanRestrictionMessage = budget.Suspended ? budget.Explanation : null;
        model.Health = budget.Health;
        if (budget.Goal != null) { model.TargetWeightKg = budget.Goal.TargetWeight; ModelState.Remove(nameof(model.TargetWeightKg)); }

    }

    private async Task<string> SaveFileAsync(IFormFile file, string subFolder)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Chỉ chấp nhận file ảnh định dạng .jpg, .jpeg, .png, .webp.");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            throw new InvalidOperationException("Kích thước file ảnh tối đa là 5 MB.");
        }

        var folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subFolder);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(folderPath, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/{subFolder}/{fileName}";
    }

    private int GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException("Người dùng chưa được xác thực hợp lệ.");
        }
        return userId;
    }
}
