using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var model = new HomeIndexViewModel
        {
            Faqs = await _context.Articles
                .AsNoTracking()
                .Where(a => a.Type == ArticleType.Faq && a.IsPublished)
                .OrderBy(a => a.Id)
                .Take(4)
                .ToListAsync(),
            PublicMealPlans = await _context.MealPlans
                .AsNoTracking()
                .Where(plan => plan.IsTemplate && plan.IsPublic)
                .OrderByDescending(plan => plan.CreatedAt)
                .ThenByDescending(plan => plan.Id)
                .Take(6)
                .Select(plan => new HomeMealPlanPreview(
                    plan.Id, plan.Name, plan.GoalType, plan.TargetCalories, plan.Explanation, plan.Items.Count,
                    plan.Items.Where(item => !item.Food.IsHidden)
                        .OrderBy(item => item.SortOrder)
                        .ThenBy(item => item.Id)
                        .Select(item => new HomeMealPlanPhoto(item.Food.Name, item.Food.ImageUrl))
                        .FirstOrDefault()))
                .ToListAsync(),
            TotalFoods = await _context.Foods.CountAsync(food => !food.IsHidden),
            TotalExercises = await _context.Exercises.CountAsync()
        };

        return View(model);
    }

    public async Task<IActionResult> Articles()
    {
        var articles = await _context.Articles
            .Where(a => a.IsPublished)
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Id)
            .ToListAsync();

        return View(articles);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
