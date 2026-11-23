using NutriTrackAI.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Filters;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Controllers;

[Authorize]
[RequireOnboarding]
public class WaterExerciseController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<WaterExerciseController> _logger;

    public WaterExerciseController(AppDbContext context, ILogger<WaterExerciseController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date)
    {
        // Chống lỗ hổng IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userId = GetCurrentUserId();
        var selectedDate = (date ?? GoalPolicy.Today).Date;

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        var waterLogs = await _context.WaterLogs
            .Where(w => w.UserId == userId && w.LoggedOn == selectedDate)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        var totalWater = waterLogs.Sum(w => w.AmountMl);
        var dailyWaterGoal = profile.DailyWaterGoalMl > 0 ? profile.DailyWaterGoalMl : 2000;
        var waterPercent = (int)Math.Clamp((totalWater * 100.0 / dailyWaterGoal), 0, 100);

        var exerciseLogs = await _context.ExerciseLogs
            .Include(e => e.Exercise)
            .Where(e => e.UserId == userId && e.LoggedOn == selectedDate)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var availableExercises = await _context.Exercises
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync();

        var model = new WaterExerciseIndexViewModel
        {
            SelectedDate = selectedDate,
            WaterLogs = waterLogs,
            TotalWaterMl = totalWater,
            DailyWaterGoalMl = dailyWaterGoal,
            WaterProgressPercent = waterPercent,
            ExerciseLogs = exerciseLogs,
            TotalExerciseDurationMin = exerciseLogs.Sum(e => e.DurationMin),
            TotalCaloriesBurned = exerciseLogs.Sum(e => e.CaloriesBurned),
            AvailableExercises = availableExercises,
            UserWeightKg = profile.CurrentWeightKg > 0 ? profile.CurrentWeightKg : 70m,
            WaterForm = new AddWaterLogModel { LoggedOn = selectedDate, AmountMl = 250 },
            ExerciseForm = new AddExerciseLogModel { LoggedOn = selectedDate, DurationMin = 30 }
        };

        return View(model);
    }

    // ==========================================
    // NHẬT KÝ NƯỚC (WATER LOGS)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddWater(AddWaterLogModel form)
    {
        var userId = GetCurrentUserId();
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        if (form.AmountMl < 50 || form.AmountMl > 2000)
        {
            TempData["WaterError"] = "Lượng nước mỗi lần phải từ 50 đến 2.000 ml.";
            return RedirectToAction(nameof(Index), new { date = form.LoggedOn.ToString("yyyy-MM-dd") });
        }

        var log = new WaterLog
        {
            UserId = userId,
            LoggedOn = form.LoggedOn.Date,
            AmountMl = form.AmountMl,
            DailyGoalMl = profile.DailyWaterGoalMl,
            CreatedAt = DateTime.UtcNow
        };
        _context.WaterLogs.Add(log);
        await _context.SaveChangesAsync();

        TempData["WaterSuccess"] = $"Đã ghi nhận thêm +{form.AmountMl} ml nước!";
        return RedirectToAction(nameof(Index), new { date = form.LoggedOn.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickWater(int amountMl, DateTime? date)
    {
        var selectedDate = (date ?? GoalPolicy.Today).Date;
        return await AddWater(new AddWaterLogModel { LoggedOn = selectedDate, AmountMl = amountMl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWater(int id, DateTime? date)
    {
        var userId = GetCurrentUserId();

        // IDOR Protection: Luôn có điều kiện UserId == userId
        var log = await _context.WaterLogs.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (log != null)
        {
            _context.WaterLogs.Remove(log);
            await _context.SaveChangesAsync();
            TempData["WaterSuccess"] = "Đã xóa bản ghi uống nước.";
        }

        var selectedDate = (date ?? GoalPolicy.Today).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Index), new { date = selectedDate });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWaterGoal(UpdateWaterGoalModel form, DateTime? date)
    {
        var userId = GetCurrentUserId();
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile != null && form.DailyWaterGoalMl >= 500 && form.DailyWaterGoalMl <= 10000)
        {
            profile.DailyWaterGoalMl = form.DailyWaterGoalMl;
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            TempData["WaterSuccess"] = $"Đã cập nhật mục tiêu nước hằng ngày thành {form.DailyWaterGoalMl:N0} ml!";
        }

        var selectedDate = (date ?? GoalPolicy.Today).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Index), new { date = selectedDate });
    }

    // ==========================================
    // NHẬT KÝ VẬN ĐỘNG (EXERCISE LOGS)
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExercise(AddExerciseLogModel form)
    {
        var userId = GetCurrentUserId();
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        if (form.DurationMin < 1 || form.DurationMin > 300)
        {
            TempData["ExerciseError"] = "Thời gian vận động phải từ 1 đến 300 phút.";
            return RedirectToAction(nameof(Index), new { date = form.LoggedOn.ToString("yyyy-MM-dd") });
        }

        if (!ModelState.IsValid || !Enum.IsDefined(form.Intensity))
        {
            TempData["ExerciseError"] = "Vui lòng kiểm tra thông tin và chọn cường độ Nhẹ, Vừa hoặc Nặng.";
            return RedirectToAction(nameof(Index), new { date = form.LoggedOn.ToString("yyyy-MM-dd") });
        }

        decimal baseMet = 5m;
        string? activityName = form.CustomName;

        if (form.ExerciseId.HasValue)
        {
            var exercise = await _context.Exercises.FindAsync(form.ExerciseId.Value);
            if (exercise == null || !exercise.IsActive)
            {
                TempData["ExerciseError"] = "Bài tập đã chọn không còn khả dụng. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Index), new { date = form.LoggedOn.ToString("yyyy-MM-dd") });
            }
            baseMet = exercise.MetValue;
            activityName = exercise.Name;
        }

        // Trừ 1 MET để bản ghi mới chỉ tính phần tiêu hao cao hơn mức nghỉ.
        var userWeight = profile.CurrentWeightKg > 0 ? profile.CurrentWeightKg : 70m;
        var met = ExerciseEnergy.EffectiveMet(baseMet, form.Intensity);
        var caloriesBurned = ExerciseEnergy.NetCalories(met, userWeight, form.DurationMin);

        var log = new ExerciseLog
        {
            UserId = userId,
            LoggedOn = form.LoggedOn.Date,
            ExerciseId = form.ExerciseId,
            CustomName = activityName,
            DurationMin = form.DurationMin,
            Intensity = form.Intensity,
            CaloriesBurned = caloriesBurned,
            CreatedAt = DateTime.UtcNow
        };

        _context.ExerciseLogs.Add(log);
        await _context.SaveChangesAsync();

        TempData["ExerciseSuccess"] = $"Đã ghi nhận vận động: {activityName} ({form.DurationMin} phút, tiêu hao ~{caloriesBurned:0.0} kcal)!";
        return RedirectToAction(nameof(Index), new { date = form.LoggedOn.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExercise(int id, DateTime? date)
    {
        var userId = GetCurrentUserId();

        // IDOR Protection: Luôn có điều kiện UserId == userId
        var log = await _context.ExerciseLogs.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
        if (log != null)
        {
            _context.ExerciseLogs.Remove(log);
            await _context.SaveChangesAsync();
            TempData["ExerciseSuccess"] = "Đã xóa bản ghi vận động.";
        }

        var selectedDate = (date ?? GoalPolicy.Today).ToString("yyyy-MM-dd");
        return RedirectToAction(nameof(Index), new { date = selectedDate });
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
