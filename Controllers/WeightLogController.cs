using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Filters;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

[Authorize]
[RequireOnboarding]
public class WeightLogController : Controller
{
    private const int HistoryPageSize = 15;
    private readonly AppDbContext _context;
    private readonly INutritionCalculatorService _calculator;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<WeightLogController> _logger;
    private readonly CalorieBudgetService _budgets;
    private readonly IWeightTrackingService _weights;

    public WeightLogController(
        AppDbContext context,
        INutritionCalculatorService calculator,
        IWebHostEnvironment webHostEnvironment,
        ILogger<WeightLogController> logger, CalorieBudgetService budgets, IWeightTrackingService weights)
    {
        _context = context;
        _calculator = calculator;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
        _budgets = budgets;
        _weights = weights;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        // Chống lỗi IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userId = GetCurrentUserId();

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        return await RenderIndexAsync(profile, page);
    }

    private async Task<ViewResult> RenderIndexAsync(UserProfile profile, int page, WeightLogFormModel? form = null)
    {
        var userId = profile.UserId;
        var activeGoal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);

        var history = _context.WeightLogs.AsNoTracking().Where(w => w.UserId == userId);
        var chartLogs = await history
            .OrderBy(w => w.LoggedOn)
            .ThenBy(w => w.Id)
            .Select(w => new { w.LoggedOn, w.WeightKg })
            .ToListAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(chartLogs.Count / (decimal)HistoryPageSize));
        var currentPage = Math.Clamp(page, 1, totalPages);
        var logs = await history
            .OrderByDescending(w => w.LoggedOn)
            .ThenByDescending(w => w.Id)
            .Skip((currentPage - 1) * HistoryPageSize)
            .Take(HistoryPageSize)
            .ToListAsync();

        var latest = await _weights.GetLatestAsync(userId);
        var currentWeight = latest?.WeightKg ?? profile.CurrentWeightKg;
        var bmiInfo = _calculator.GetBmiInfo(currentWeight, profile.HeightCm, _calculator.CalculateAge(profile.DateOfBirth));

        var targetWeight = activeGoal?.TargetWeight ?? profile.TargetWeightKg;

        var model = new WeightLogIndexViewModel
        {
            Logs = logs,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            CurrentWeight = currentWeight,
            WeightMismatch = WeightMismatchViewModel.From(profile.CurrentWeightKg, latest),
            TargetWeight = targetWeight,
            StartWeight = activeGoal?.StartWeight,
            Bmi = bmiInfo.Bmi,
            BmiCategory = bmiInfo.Category,
            BmiBadgeClass = bmiInfo.BadgeClass,
            ChartDates = chartLogs.Select(l => l.LoggedOn.ToString("dd/MM")).ToList(),
            ChartWeights = chartLogs.Select(l => l.WeightKg).ToList(),
            ChartTargets = chartLogs.Select(_ => targetWeight).ToList(),
            Form = form ?? new WeightLogFormModel
            {
                LoggedOn = GoalPolicy.Today,
                WeightKg = currentWeight
            }
        };

        if (chartLogs.Any())
        {
            var firstWeight = chartLogs.First().WeightKg;
            model.WeightChange = currentWeight - firstWeight;
        }

        return View(nameof(Index), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WeightLogFormModel form, int page = 1)
    {
        var userId = GetCurrentUserId();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin cân nặng không hợp lệ (Cân nặng từ 20 đến 300 kg).";
            return RedirectToHistory(page);
        }

        if (form.LoggedOn.Date > GoalPolicy.Today) return BadRequest("Không ghi cân nặng trong tương lai.");
        var day = form.LoggedOn.Date;
        WeightLog? edited = null;
        if (form.Id > 0)
        {
            edited = await _context.WeightLogs.FirstOrDefaultAsync(log => log.Id == form.Id && log.UserId == userId);
            if (edited == null) return NotFound();

            if (edited.LoggedOn.Date != day && await _context.WeightLogs.AnyAsync(log =>
                    log.UserId == userId && log.LoggedOn >= day && log.LoggedOn < day.AddDays(1)))
            {
                ModelState.AddModelError("Form.LoggedOn", "Ngày này đã có bản ghi cân nặng. Hãy chọn bản ghi đó để sửa.");
                return await RenderIndexAsync(profile, page, form);
            }
        }

        var reference = edited?.LoggedOn.Date == day ? edited : await _weights.GetLatestAsync(userId, day, edited?.Id);
        if (!form.ConfirmLargeWeightChange && WeightTrackingService.LargeChangeWarning(reference, day, form.WeightKg) is { } warning)
        {
            ModelState.AddModelError("Form.ConfirmLargeWeightChange", warning);
            return await RenderIndexAsync(profile, page, form);
        }

        string? photoPath = null;
        if (form.PhotoFile != null && form.PhotoFile.Length > 0)
        {
            try
            {
                photoPath = await SaveProgressPhotoAsync(form.PhotoFile);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToHistory(page);
            }
        }

        await _weights.RecordWeightAsync(profile, day, form.WeightKg, form.Note, photoPath, edited);
        var budgetMessage = await _budgets.RefreshAsync(userId);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["SuccessMessage"] = $"Đã ghi nhận cân nặng {form.WeightKg:0.0} kg. {budgetMessage}";
        return RedirectToHistory(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int page = 1)
    {
        var userId = GetCurrentUserId();
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        // IDOR Protection: Luôn có điều kiện UserId == userId
        var log = await _context.WeightLogs.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (log != null)
        {
            await _weights.DeleteAsync(profile, log);
            var budgetMessage = await _budgets.RefreshAsync(userId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["SuccessMessage"] = $"Đã xóa bản ghi cân nặng. {budgetMessage}";
        }

        return RedirectToHistory(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReconcileUseProfile(decimal? expectedProfileWeight, decimal? expectedLogWeight,
        DateTime? expectedLogDate, string? returnTo, int page = 1) =>
        ReconcileAsync(true, expectedProfileWeight, expectedLogWeight, expectedLogDate, returnTo, page);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReconcileUseLog(decimal? expectedProfileWeight, decimal? expectedLogWeight,
        DateTime? expectedLogDate, string? returnTo, int page = 1) =>
        ReconcileAsync(false, expectedProfileWeight, expectedLogWeight, expectedLogDate, returnTo, page);

    private async Task<IActionResult> ReconcileAsync(bool useProfile, decimal? expectedProfileWeight,
        decimal? expectedLogWeight, DateTime? expectedLogDate, string? returnTo, int page)
    {
        var userId = GetCurrentUserId();
        var destination = returnTo is "Dashboard" or "Profile" ? returnTo : "WeightLog";
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        var latest = await _weights.GetLatestAsync(userId);
        if (!ModelState.IsValid || latest == null ||
            WeightTrackingService.SameWeight(profile.CurrentWeightKg, latest.WeightKg) ||
            expectedProfileWeight != profile.CurrentWeightKg || expectedLogWeight != latest.WeightKg ||
            expectedLogDate != latest.LoggedOn.Date)
        {
            TempData["ErrorMessage"] = "Dữ liệu cân nặng đã thay đổi. Hãy kiểm tra lại thông tin hiện tại.";
            return RedirectToHistory(page, destination);
        }

        if (useProfile)
            await _weights.RecordWeightAsync(profile, GoalPolicy.Today, profile.CurrentWeightKg, "Cập nhật từ Hồ sơ");
        else
            _weights.UseLatestWeight(profile, latest);

        var budgetMessage = await _budgets.RefreshAsync(userId);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        var message = useProfile ? "Đã ghi cân nặng hồ sơ vào nhật ký hôm nay." : "Đã dùng cân nặng từ bản ghi gần nhất.";
        TempData["SuccessMessage"] = $"{message} {budgetMessage}";
        return RedirectToHistory(page, destination);
    }

    private RedirectToActionResult RedirectToHistory(int page, string controller = "WeightLog") =>
        RedirectToAction(nameof(Index), controller, controller == "WeightLog" && page > 1 ? new { page } : null);

    private async Task<string> SaveProgressPhotoAsync(IFormFile file)
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

        var folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "progress");
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

        return $"/uploads/progress/{fileName}";
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
