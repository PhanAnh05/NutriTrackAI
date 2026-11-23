using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Filters;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

[Authorize]
[RequireOnboarding]
public class MealPlanController : Controller
{
    private readonly AppDbContext _context;
    private readonly IMealPlannerService _mealPlanner;
    private readonly INutritionCalculatorService _nutritionCalculator;
    private readonly ILogger<MealPlanController> _logger;
    private readonly CalorieBudgetService _budgets;

    public MealPlanController(
        AppDbContext context,
        IMealPlannerService mealPlanner,
        INutritionCalculatorService nutritionCalculator,
        ILogger<MealPlanController> logger, CalorieBudgetService budgets)
    {
        _context = context;
        _mealPlanner = mealPlanner;
        _nutritionCalculator = nutritionCalculator;
        _logger = logger;
        _budgets = budgets;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? planId)
    {
        var userId = GetCurrentUserId();

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        var budget = await _budgets.GetAsync(userId);
        var goalType = budget.Goal?.GoalType ?? GoalType.Maintain;
        var defaultTargetCal = budget.TargetCalories;
        var constraints = budget.Health.Constraints;

        MealPlanResult? currentPlan = null;

        // Nếu người dùng chọn xem 1 thực đơn cụ thể
        if (planId.HasValue)
        {
            currentPlan = await _mealPlanner.GetPlanByIdAsync(planId.Value, userId);
        }
        else if (TempData["CurrentPlanJson"] is string planJson && !string.IsNullOrWhiteSpace(planJson))
        {
            try
            {
                currentPlan = JsonSerializer.Deserialize<MealPlanResult>(planJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể deserialize CurrentPlanJson");
            }
        }

        // Nếu chưa có thực đơn nào được tải, tự động sinh thực đơn cá nhân hóa lần đầu
        if (currentPlan == null && !budget.Suspended)
        {
            try
            {
                currentPlan = await _mealPlanner.GeneratePlanAsync(userId, defaultTargetCal, 4);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tự động sinh thực đơn cho User {UserId}", userId);
            }
        }

        if (!budget.Suspended && currentPlan != null &&
            (currentPlan.TargetCalories != defaultTargetCal || (!planId.HasValue && currentPlan.Meals.Count < constraints.MinimumMeals)))
        {
            TempData["SuccessMessage"] = "Ngân sách đã thay đổi. Đang hiển thị gợi ý mới; thực đơn đã lưu và nhật ký cũ được giữ nguyên.";
            currentPlan = await _mealPlanner.GeneratePlanAsync(userId, null, 4);
        }
        var publicTemplates = await _mealPlanner.GetPublicTemplatesAsync();
        var savedUserPlans = await _mealPlanner.GetUserSavedPlansAsync(userId);

        var model = new MealPlanIndexViewModel
        {
            CurrentPlan = currentPlan,
            DietaryConstraints = constraints,
            RecommendedMacros = _budgets.GetMacros(profile, budget),
            PublicTemplates = publicTemplates,
            SavedUserPlans = savedUserPlans,
            GenerateInput = new GeneratePlanInputModel
            {
                TargetCalories = defaultTargetCal,
                NumberOfMeals = 4,
                GoalType = goalType,
                DietType = profile.DietType
            }
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(GeneratePlanInputModel input)
    {
        var userId = GetCurrentUserId();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông số sinh thực đơn chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var plan = await _mealPlanner.GeneratePlanAsync(userId, null, input.NumberOfMeals);
            TempData["CurrentPlanJson"] = JsonSerializer.Serialize(plan);
            TempData["SuccessMessage"] = "Đã sinh thực đơn cá nhân hóa mới thành công!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi sinh thực đơn cho User {UserId}", userId);
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetSwapCandidates(int foodId, decimal servings, [FromServices] IWebHostEnvironment environment)
    {
        var userId = GetCurrentUserId();
        var candidates = await _mealPlanner.GetSwapCandidatesAsync(foodId, servings, userId);
        foreach (var food in candidates)
        {
            food.ImageUrl = FoodImageUrl.Resolve(food.FoodName, food.ImageUrl, environment.WebRootFileProvider);
        }
        return Json(candidates);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlan(string planName, string planJson)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(planJson))
        {
            TempData["ErrorMessage"] = "Không tìm thấy dữ liệu thực đơn để lưu.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var planResult = JsonSerializer.Deserialize<MealPlanResult>(planJson);
            if (planResult == null) throw new InvalidOperationException("Dữ liệu thực đơn không hợp lệ.");

            var planId = await _mealPlanner.SavePlanAsync(userId, planName, planResult);
            TempData["SuccessMessage"] = $"Đã lưu thực đơn '{planName}' thành công!";
            return RedirectToAction(nameof(Index), new { planId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu thực đơn cho User {UserId}", userId);
            TempData["ErrorMessage"] = "Không thể lưu thực đơn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyToToday(string planJson)
    {
        var userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(planJson))
        {
            TempData["ErrorMessage"] = "Không tìm thấy dữ liệu thực đơn để áp dụng.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var planResult = JsonSerializer.Deserialize<MealPlanResult>(planJson);
            if (planResult == null) throw new InvalidOperationException("Dữ liệu thực đơn không hợp lệ.");

            await _mealPlanner.ApplyPlanToTodayAsync(userId, planResult, GoalPolicy.Today);
            TempData["SuccessMessage"] = "Đã thêm toàn bộ các món trong thực đơn vào Nhật ký bữa ăn hôm nay!";
            return RedirectToAction("Index", "MealLog");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi áp dụng thực đơn vào Nhật ký cho User {UserId}", userId);
            TempData["ErrorMessage"] = "Không thể áp dụng thực đơn vào nhật ký. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSavedPlan(int planId)
    {
        var userId = GetCurrentUserId();

        // Chống lỗ hổng IDOR: Đảm bảo thực đơn thuộc về chính User này
        var plan = await _context.MealPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == planId && p.UserId == userId);

        if (plan != null)
        {
            _context.MealPlanItems.RemoveRange(plan.Items);
            _context.MealPlans.Remove(plan);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa thực đơn '{plan.Name}'.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không tìm thấy thực đơn hoặc bạn không có quyền xóa.";
        }

        return RedirectToAction(nameof(Index));
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
