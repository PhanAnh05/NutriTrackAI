using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class FoodsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public FoodsController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? categoryId, bool? showHidden)
    {
        var query = _context.Foods
            .Include(f => f.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(f => f.Name.ToLower().Contains(s) || (f.Ingredients != null && f.Ingredients.ToLower().Contains(s)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(f => f.CategoryId == categoryId.Value);
        }

        if (showHidden.HasValue)
        {
            query = query.Where(f => f.IsHidden == showHidden.Value);
        }

        var foods = await query.OrderBy(f => f.CategoryId).ThenBy(f => f.Name).ToListAsync();
        ViewBag.Categories = await _context.FoodCategories.OrderBy(c => c.Id).ToListAsync();
        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.ShowHidden = showHidden;

        return View(foods);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var categories = await _context.FoodCategories.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        var model = new AdminFoodEditViewModel
        {
            AvailableCategories = categories
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminFoodEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableCategories = await _context.FoodCategories.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
            return View(model);
        }

        string? imageUrl = model.ImageUrl;

        // Xử lý upload ảnh nếu có
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExts.Contains(ext) || model.ImageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("ImageFile", "Ảnh chỉ chấp nhận định dạng .jpg, .png, .webp và dung lượng tối đa 5MB.");
                model.AvailableCategories = await _context.FoodCategories.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
                return View(model);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "foods");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = $"food_{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }
            imageUrl = $"/uploads/foods/{fileName}";
        }

        var food = new Food
        {
            Name = model.Name.Trim(),
            CategoryId = model.CategoryId,
            ServingName = model.ServingName.Trim(),
            ServingSize = model.ServingSize,
            Calories = model.Calories,
            Protein = model.Protein,
            Carb = model.Carb,
            Fat = model.Fat,
            Fiber = model.Fiber,
            Sugar = model.Sugar,
            Sodium = model.Sodium,
            Ingredients = model.Ingredients?.Trim(),
            AllergenNotes = model.AllergenNotes?.Trim(),
            HealthWarning = model.HealthWarning?.Trim(),
            IsVegetarian = model.IsVegetarian,
            IsHidden = model.IsHidden,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Foods.Add(food);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm món mới '{food.Name}' thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var food = await _context.Foods.FindAsync(id);
        if (food == null) return NotFound();

        var categories = await _context.FoodCategories.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        var model = new AdminFoodEditViewModel
        {
            Id = food.Id,
            Name = food.Name,
            CategoryId = food.CategoryId,
            ServingName = food.ServingName,
            ServingSize = food.ServingSize,
            Calories = food.Calories,
            Protein = food.Protein,
            Carb = food.Carb,
            Fat = food.Fat,
            Fiber = food.Fiber,
            Sugar = food.Sugar,
            Sodium = food.Sodium,
            Ingredients = food.Ingredients,
            AllergenNotes = food.AllergenNotes,
            HealthWarning = food.HealthWarning,
            IsVegetarian = food.IsVegetarian,
            IsHidden = food.IsHidden,
            ImageUrl = food.ImageUrl,
            AvailableCategories = categories
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminFoodEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableCategories = await _context.FoodCategories.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
            return View(model);
        }

        var food = await _context.Foods.FindAsync(model.Id);
        if (food == null) return NotFound();

        // Xử lý upload ảnh mới nếu có
        if (model.ImageFile != null && model.ImageFile.Length > 0)
        {
            var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowedExts.Contains(ext) || model.ImageFile.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("ImageFile", "Ảnh chỉ chấp nhận định dạng .jpg, .png, .webp và dung lượng tối đa 5MB.");
                model.AvailableCategories = await _context.FoodCategories.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
                return View(model);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "foods");
            Directory.CreateDirectory(uploadsFolder);
            var fileName = $"food_{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ImageFile.CopyToAsync(stream);
            }
            food.ImageUrl = $"/uploads/foods/{fileName}";
        }

        food.Name = model.Name.Trim();
        food.CategoryId = model.CategoryId;
        food.ServingName = model.ServingName.Trim();
        food.ServingSize = model.ServingSize;
        food.Calories = model.Calories;
        food.Protein = model.Protein;
        food.Carb = model.Carb;
        food.Fat = model.Fat;
        food.Fiber = model.Fiber;
        food.Sugar = model.Sugar;
        food.Sodium = model.Sodium;
        food.Ingredients = model.Ingredients?.Trim();
        food.AllergenNotes = model.AllergenNotes?.Trim();
        food.HealthWarning = model.HealthWarning?.Trim();
        food.IsVegetarian = model.IsVegetarian;
        food.IsHidden = model.IsHidden;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật món '{food.Name}' thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHidden(int id)
    {
        var food = await _context.Foods.FindAsync(id);
        if (food == null) return NotFound();

        food.IsHidden = !food.IsHidden;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = food.IsHidden
            ? $"Đã ẩn món '{food.Name}' khỏi danh mục người dùng."
            : $"Đã hiển thị lại món '{food.Name}' trên hệ thống.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var food = await _context.Foods.FindAsync(id);
        if (food == null) return NotFound();

        // Kiểm tra xem món đã nằm trong nhật ký ăn của User hoặc thực đơn chưa
        var hasLogs = await _context.MealLogDetails.AnyAsync(d => d.FoodId == id);
        var hasPlans = await _context.MealPlanItems.AnyAsync(p => p.FoodId == id);

        if (hasLogs || hasPlans)
        {
            food.IsHidden = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Món '{food.Name}' đã được ghi nhận trong dữ liệu người dùng nên hệ thống đã tự động chuyển sang chế độ ẨN thay vì xóa cứng để bảo đảm toàn vẹn dữ liệu.";
            return RedirectToAction(nameof(Index));
        }

        _context.Foods.Remove(food);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã xóa món '{food.Name}' khỏi cơ sở dữ liệu.";
        return RedirectToAction(nameof(Index));
    }
}
