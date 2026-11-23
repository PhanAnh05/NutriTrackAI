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
public class MealLogController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<MealLogController> _logger;
    private readonly CalorieBudgetService _budgets;

    public MealLogController(AppDbContext context, ILogger<MealLogController> logger, CalorieBudgetService budgets)
    {
        _context = context;
        _logger = logger;
        _budgets = budgets;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? date)
    {
        // Chống lỗi IDOR: Luôn lấy userId từ Claims của phiên đăng nhập hiện tại
        var userId = GetCurrentUserId();

        // Không cho phép chọn ngày tương lai
        var today = GoalPolicy.Today;
        var selectedDate = (date ?? today).Date;
        if (selectedDate > today)
        {
            selectedDate = today;
        }

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return RedirectToAction("Index", "Onboarding");

        var budget = await _budgets.GetAsync(userId);
        var dailyTargetCalories = budget.TargetCalories;

        // Calo vận động tiêu hao trong ngày
        var caloriesBurned = await _context.ExerciseLogs
            .Where(e => e.UserId == userId && e.LoggedOn == selectedDate)
            .SumAsync(e => e.CaloriesBurned);

        // Lấy 4 bữa ăn trong ngày
        var mealLogs = await _context.MealLogs
            .Include(m => m.Details)
                .ThenInclude(d => d.Food)
                    .ThenInclude(f => f.Category)
            .Where(m => m.UserId == userId && m.LoggedOn == selectedDate)
            .ToListAsync();

        var breakfast = mealLogs.FirstOrDefault(m => m.MealType == MealType.Breakfast);
        var lunch = mealLogs.FirstOrDefault(m => m.MealType == MealType.Lunch);
        var dinner = mealLogs.FirstOrDefault(m => m.MealType == MealType.Dinner);
        var snack = mealLogs.FirstOrDefault(m => m.MealType == MealType.Snack);

        // Tính tổng dinh dưỡng toàn ngày
        var allDetails = mealLogs.SelectMany(m => m.Details).ToList();
        var totalCal = allDetails.Sum(d => d.Calories);
        var totalP = allDetails.Sum(d => d.Protein);
        var totalC = allDetails.Sum(d => d.Carb);
        var totalF = allDetails.Sum(d => d.Fat);
        var totalFiber = allDetails.Sum(d => d.Fiber);
        var totalSugar = allDetails.Sum(d => d.Sugar);
        var totalSodium = allDetails.Sum(d => d.Sodium);

        // Công thức calo còn lại theo Mục 4.5 GEMINI.md:
        var remainingCalories = GoalPolicy.RemainingCalories(dailyTargetCalories, totalCal, caloriesBurned);

        // Trạng thái calo theo ngưỡng (Mục 4.5 GEMINI.md)
        string statusClass;
        string statusBadgeBg;
        if (remainingCalories > 50)
        {
            statusClass = "text-success";
            statusBadgeBg = "bg-success";
        }
        else if (remainingCalories >= -50 && remainingCalories <= 50)
        {
            statusClass = "text-warning-emphasis";
            statusBadgeBg = "bg-warning";
        }
        else
        {
            statusClass = "text-danger";
            statusBadgeBg = "bg-danger";
        }

        // Kiểm tra dị ứng đối chiếu với hồ sơ người dùng trên tất cả món đã ăn
        var allergyAlerts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.Allergies))
        {
            var restrictions = new FoodRestrictions(FoodRestrictions.Split(profile.Allergies));

            foreach (var detail in allDetails)
            {
                var matched = restrictions.MatchedAllergies(detail.Food);
                if (matched.Any())
                {
                    var msg = $"Món '{detail.Food.Name}' có chứa [{string.Join(", ", matched)}] trùng với hồ sơ dị ứng của bạn!";
                    if (!allergyAlerts.Contains(msg))
                    {
                        allergyAlerts.Add(msg);
                    }
                }
            }
        }

        // Lấy danh sách món yêu thích của User để hỗ trợ chọn nhanh
        var favoriteFoods = await _context.Favorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Food)
                .ThenInclude(food => food.Category)
            .Select(f => f.Food)
            .ToListAsync();

        var categories = await _context.FoodCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var model = new MealLogIndexViewModel
        {
            SelectedDate = selectedDate,
            BreakfastLog = breakfast,
            LunchLog = lunch,
            DinnerLog = dinner,
            SnackLog = snack,
            TotalCalories = totalCal,
            TotalProtein = totalP,
            TotalCarb = totalC,
            TotalFat = totalF,
            TotalFiber = totalFiber,
            TotalSugar = totalSugar,
            TotalSodium = totalSodium,
            DietaryConstraints = budget.Health.Constraints,
            RecommendedMacros = _budgets.GetMacros(profile, budget),
            DailyCalorieTarget = dailyTargetCalories,
            CaloriesBurnedToday = caloriesBurned,
            ExerciseCreditApplied = GoalPolicy.ExerciseCredit(caloriesBurned),
            RemainingCalories = remainingCalories,
            CalorieStatusClass = statusClass,
            UsesMaintenanceReference = budget.UsesMaintenanceReference,
            CalorieReferenceLabel = budget.ReferenceLabel,
            CalorieStatusBadgeBg = statusBadgeBg,
            AllergyAlerts = allergyAlerts,
            FavoriteFoods = favoriteFoods,
            Categories = categories
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFood(AddMealFoodInputModel model)
    {
        var userId = GetCurrentUserId();

        // Không cho phép ghi nhận ngày tương lai
        if (model.LoggedOn.Date > GoalPolicy.Today)
        {
            TempData["ErrorMessage"] = "Không thể ghi nhận bữa ăn cho ngày tương lai.";
            return RedirectToAction(nameof(Index));
        }

        if (model.Servings <= 0 || model.Servings > 5000)
        {
            TempData["ErrorMessage"] = "Định lượng khẩu phần phải lớn hơn 0 và không vượt quá 5.000.";
            return RedirectToAction(nameof(Index), new { date = model.LoggedOn.ToString("yyyy-MM-dd") });
        }

        var food = await _context.Foods.FirstOrDefaultAsync(f => f.Id == model.FoodId && !f.IsHidden);
        if (food == null)
        {
            TempData["ErrorMessage"] = "Món ăn không tồn tại hoặc đã bị ẩn.";
            return RedirectToAction(nameof(Index), new { date = model.LoggedOn.ToString("yyyy-MM-dd") });
        }

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        // Kiểm tra dị ứng nếu người dùng chưa bấm xác nhận
        if (!model.ConfirmedAllergy && profile != null && !string.IsNullOrWhiteSpace(profile.Allergies))
        {
            var matched = new FoodRestrictions(FoodRestrictions.Split(profile.Allergies)).MatchedAllergies(food);
            if (matched.Any())
            {
                TempData["PendingAllergyWarning"] = $"Món '{food.Name}' có chứa thành phần [{string.Join(", ", matched)}] trùng với danh sách dị ứng của bạn!";
                TempData["PendingFoodId"] = food.Id;
                TempData["PendingMealType"] = model.MealType;
                TempData["PendingServings"] = model.Servings.ToString("0.##");
                TempData["PendingDate"] = model.LoggedOn.ToString("yyyy-MM-dd");
                return RedirectToAction(nameof(Index), new { date = model.LoggedOn.ToString("yyyy-MM-dd") });
            }
        }

        // Tìm hoặc tạo MealLog của bữa đó
        var mealTypeEnum = (MealType)model.MealType;
        var mealLog = await _context.MealLogs
            .FirstOrDefaultAsync(m => m.UserId == userId && m.LoggedOn == model.LoggedOn.Date && m.MealType == mealTypeEnum);

        if (mealLog == null)
        {
            mealLog = new MealLog
            {
                UserId = userId,
                LoggedOn = model.LoggedOn.Date,
                MealType = mealTypeEnum,
                CreatedAt = DateTime.UtcNow
            };
            _context.MealLogs.Add(mealLog);
            await _context.SaveChangesAsync();
        }

        // Quy đổi dinh dưỡng thực tế theo tỷ lệ: Dinh dưỡng chuẩn * (Servings nhập / ServingSize chuẩn)
        var ratio = food.ServingSize > 0 ? (model.Servings / food.ServingSize) : 1m;

        var detail = new MealLogDetail
        {
            MealLogId = mealLog.Id,
            FoodId = food.Id,
            Servings = model.Servings,
            Calories = Math.Round(food.Calories * ratio, 2),
            Protein = Math.Round(food.Protein * ratio, 2),
            Carb = Math.Round(food.Carb * ratio, 2),
            Fat = Math.Round(food.Fat * ratio, 2),
            Fiber = Math.Round(food.Fiber * ratio, 2),
            Sugar = Math.Round(food.Sugar * ratio, 2),
            Sodium = Math.Round(food.Sodium * ratio, 2)
        };

        _context.MealLogDetails.Add(detail);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm món '{food.Name}' ({model.Servings:0.##} {food.ServingName}) vào {GetMealTypeName(mealTypeEnum)}!";
        return RedirectToAction(nameof(Index), new { date = model.LoggedOn.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateServings(UpdateMealDetailInputModel model)
    {
        var userId = GetCurrentUserId();

        if (model.Servings <= 0 || model.Servings > 5000)
        {
            TempData["ErrorMessage"] = "Định lượng khẩu phần phải lớn hơn 0 và không vượt quá 5.000.";
            return RedirectToAction(nameof(Index), new { date = model.LoggedOn.ToString("yyyy-MM-dd") });
        }

        // IDOR Protection: Đảm bảo chi tiết này thuộc về bữa ăn của chính User hiện tại
        var detail = await _context.MealLogDetails
            .Include(d => d.Food)
            .Include(d => d.MealLog)
            .FirstOrDefaultAsync(d => d.Id == model.DetailId && d.MealLog.UserId == userId);

        if (detail != null)
        {
            var food = detail.Food;
            var ratio = food.ServingSize > 0 ? (model.Servings / food.ServingSize) : 1m;

            detail.Servings = model.Servings;
            detail.Calories = Math.Round(food.Calories * ratio, 2);
            detail.Protein = Math.Round(food.Protein * ratio, 2);
            detail.Carb = Math.Round(food.Carb * ratio, 2);
            detail.Fat = Math.Round(food.Fat * ratio, 2);
            detail.Fiber = Math.Round(food.Fiber * ratio, 2);
            detail.Sugar = Math.Round(food.Sugar * ratio, 2);
            detail.Sodium = Math.Round(food.Sodium * ratio, 2);

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật định lượng món '{food.Name}' thành {model.Servings:0.##}!";
        }

        return RedirectToAction(nameof(Index), new { date = model.LoggedOn.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDetail(int detailId, DateTime date)
    {
        var userId = GetCurrentUserId();

        // IDOR Protection: Kiểm tra quyền sở hữu bản ghi qua MealLog.UserId == userId
        var detail = await _context.MealLogDetails
            .Include(d => d.Food)
            .Include(d => d.MealLog)
            .FirstOrDefaultAsync(d => d.Id == detailId && d.MealLog.UserId == userId);

        if (detail != null)
        {
            var foodName = detail.Food.Name;
            _context.MealLogDetails.Remove(detail);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa món '{foodName}' khỏi bữa ăn.";
        }

        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
    }

    [HttpGet]
    public async Task<IActionResult> CheckFoodAllergy(int foodId)
    {
        var userId = GetCurrentUserId();
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var food = await _context.Foods.FindAsync(foodId);

        if (profile == null || food == null || string.IsNullOrWhiteSpace(profile.Allergies))
        {
            return Json(new { hasAllergy = false });
        }

        var matched = new FoodRestrictions(FoodRestrictions.Split(profile.Allergies)).MatchedAllergies(food);

        return Json(new
        {
            hasAllergy = matched.Any(),
            allergensFound = string.Join(", ", matched),
            foodName = food.Name
        });
    }

    private string GetMealTypeName(MealType type) => type switch
    {
        MealType.Breakfast => "Bữa sáng",
        MealType.Lunch => "Bữa trưa",
        MealType.Dinner => "Bữa tối",
        MealType.Snack => "Bữa phụ",
        _ => "Bữa ăn"
    };

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
