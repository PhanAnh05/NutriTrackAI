using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class MealPlansController : Controller
{
    private readonly AppDbContext _context;

    public MealPlansController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var templates = await _context.MealPlans
            .Include(p => p.Items)
                .ThenInclude(i => i.Food)
            .Where(p => p.IsTemplate)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(templates);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AdminMealPlanEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminMealPlanEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var plan = new MealPlan
        {
            UserId = null,
            Name = model.Name.Trim(),
            GoalType = model.GoalType,
            TargetCalories = model.TargetCalories,
            IsTemplate = true,
            IsPublic = model.IsPublic,
            Explanation = model.Explanation?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.MealPlans.Add(plan);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo thực đơn mẫu '{plan.Name}'. Tiếp theo hãy thêm các món ăn vào từng bữa.";
        return RedirectToAction(nameof(Details), new { id = plan.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var plan = await _context.MealPlans
            .Include(p => p.Items)
                .ThenInclude(i => i.Food)
                    .ThenInclude(f => f.Category)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsTemplate);

        if (plan == null) return NotFound();

        ViewBag.Foods = await _context.Foods
            .Where(f => !f.IsHidden)
            .OrderBy(f => f.CategoryId)
            .ThenBy(f => f.Name)
            .ToListAsync();

        return View(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(int mealPlanId, int foodId, int mealType, decimal servings)
    {
        var plan = await _context.MealPlans.FirstOrDefaultAsync(p => p.Id == mealPlanId && p.IsTemplate);
        if (plan == null) return NotFound();

        var food = await _context.Foods.FindAsync(foodId);
        if (food == null) return NotFound();

        if (servings <= 0) servings = food.ServingSize;

        var item = new MealPlanItem
        {
            MealPlanId = plan.Id,
            FoodId = food.Id,
            MealType = (MealType)mealType,
            Servings = servings,
            SortOrder = await _context.MealPlanItems.CountAsync(i => i.MealPlanId == plan.Id) + 1
        };

        _context.MealPlanItems.Add(item);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm món '{food.Name}' vào {(MealType)mealType}!";
        return RedirectToAction(nameof(Details), new { id = plan.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int itemId, int mealPlanId)
    {
        var item = await _context.MealPlanItems.FirstOrDefaultAsync(i => i.Id == itemId && i.MealPlanId == mealPlanId);
        if (item != null)
        {
            _context.MealPlanItems.Remove(item);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa món khỏi thực đơn mẫu.";
        }

        return RedirectToAction(nameof(Details), new { id = mealPlanId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublic(int id)
    {
        var plan = await _context.MealPlans.FirstOrDefaultAsync(p => p.Id == id && p.IsTemplate);
        if (plan == null) return NotFound();

        plan.IsPublic = !plan.IsPublic;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = plan.IsPublic
            ? $"Đã công khai thực đơn mẫu '{plan.Name}' cho toàn bộ người dùng."
            : $"Đã chuyển thực đơn mẫu '{plan.Name}' sang chế độ riêng tư.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await _context.MealPlans
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsTemplate);

        if (plan != null)
        {
            _context.MealPlanItems.RemoveRange(plan.Items);
            _context.MealPlans.Remove(plan);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa thực đơn mẫu '{plan.Name}'.";
        }

        return RedirectToAction(nameof(Index));
    }
}
