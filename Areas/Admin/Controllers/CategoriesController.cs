using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CategoriesController : Controller
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var categories = await _context.FoodCategories
            .Include(c => c.Foods)
            .OrderBy(c => c.Id)
            .ToListAsync();

        return View(categories);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AdminCategoryEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCategoryEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var existingSlug = await _context.FoodCategories.AnyAsync(c => c.Slug == model.Slug.Trim().ToLower());
        if (existingSlug)
        {
            ModelState.AddModelError("Slug", "Slug này đã tồn tại, vui lòng chọn slug khác.");
            return View(model);
        }

        var category = new FoodCategory
        {
            Name = model.Name.Trim(),
            Slug = model.Slug.Trim().ToLower(),
            Description = model.Description?.Trim(),
            IsActive = model.IsActive
        };

        _context.FoodCategories.Add(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo mới nhóm thực phẩm '{category.Name}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.FoodCategories.FindAsync(id);
        if (category == null) return NotFound();

        var model = new AdminCategoryEditViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            IsActive = category.IsActive
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminCategoryEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var category = await _context.FoodCategories.FindAsync(model.Id);
        if (category == null) return NotFound();

        var existingSlug = await _context.FoodCategories.AnyAsync(c => c.Slug == model.Slug.Trim().ToLower() && c.Id != model.Id);
        if (existingSlug)
        {
            ModelState.AddModelError("Slug", "Slug này đã được dùng bởi nhóm khác.");
            return View(model);
        }

        category.Name = model.Name.Trim();
        category.Slug = model.Slug.Trim().ToLower();
        category.Description = model.Description?.Trim();
        category.IsActive = model.IsActive;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật nhóm thực phẩm '{category.Name}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var category = await _context.FoodCategories.FindAsync(id);
        if (category == null) return NotFound();

        category.IsActive = !category.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = category.IsActive
            ? $"Đã kích hoạt nhóm '{category.Name}'."
            : $"Đã vô hiệu hóa nhóm '{category.Name}'.";

        return RedirectToAction(nameof(Index));
    }
}
