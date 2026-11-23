using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ArticlesController : Controller
{
    private readonly AppDbContext _context;

    public ArticlesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, ArticleType? type)
    {
        var query = _context.Articles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(s) || a.Body.ToLower().Contains(s));
        }

        if (type.HasValue)
        {
            query = query.Where(a => a.Type == type.Value);
        }

        var articles = await query.OrderBy(a => a.Type).ThenByDescending(a => a.UpdatedAt).ToListAsync();
        ViewBag.Search = search;
        ViewBag.Type = type;

        return View(articles);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new AdminArticleEditViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminArticleEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var article = new Article
        {
            Type = model.Type,
            Title = model.Title.Trim(),
            Body = model.Body.Trim(),
            IsPublished = model.IsPublished,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Articles.Add(article);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo bài viết/FAQ mới: '{article.Title}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound();

        var model = new AdminArticleEditViewModel
        {
            Id = article.Id,
            Type = article.Type,
            Title = article.Title,
            Body = article.Body,
            IsPublished = article.IsPublished
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminArticleEditViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var article = await _context.Articles.FindAsync(model.Id);
        if (article == null) return NotFound();

        article.Type = model.Type;
        article.Title = model.Title.Trim();
        article.Body = model.Body.Trim();
        article.IsPublished = model.IsPublished;
        article.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã cập nhật bài viết/FAQ: '{article.Title}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article == null) return NotFound();

        article.IsPublished = !article.IsPublished;
        article.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = article.IsPublished
            ? $"Đã xuất bản bài viết '{article.Title}' (Sẵn sàng phục vụ RAG)."
            : $"Đã gỡ bài viết '{article.Title}' về bản nháp.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var article = await _context.Articles.FindAsync(id);
        if (article != null)
        {
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa bài viết '{article.Title}'.";
        }

        return RedirectToAction(nameof(Index));
    }
}
