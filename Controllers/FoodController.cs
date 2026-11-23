using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Services;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Controllers;

public class FoodController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<FoodController> _logger;
    private readonly CalorieBudgetService _budgets;

    public FoodController(AppDbContext context, ILogger<FoodController> logger, CalorieBudgetService budgets)
    {
        _context = context;
        _logger = logger;
        _budgets = budgets;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        decimal? minCal,
        decimal? maxCal,
        bool? isVegetarian,
        int page = 1)
    {
        var query = _context.Foods
            .Include(f => f.Category)
            .Where(f => !f.IsHidden);

        // 1. Tìm kiếm theo tên hoặc nguyên liệu
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(f => f.Name.Contains(s) || (f.Ingredients != null && f.Ingredients.Contains(s)));
        }

        // 2. Lọc theo nhóm món
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(f => f.CategoryId == categoryId.Value);
        }

        // 3. Lọc theo khoảng calo
        if (minCal.HasValue && minCal.Value >= 0)
        {
            query = query.Where(f => f.Calories >= minCal.Value);
        }
        if (maxCal.HasValue && maxCal.Value > 0)
        {
            query = query.Where(f => f.Calories <= maxCal.Value);
        }

        // 4. Lọc món chay
        if (isVegetarian.HasValue && isVegetarian.Value)
        {
            query = query.Where(f => f.IsVegetarian);
        }

        var totalItems = await query.CountAsync();
        const int pageSize = 12;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var foods = await query
            .OrderBy(f => f.CategoryId)
            .ThenBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var categories = await _context.FoodCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .ToListAsync();

        var favoriteFoodIds = new HashSet<int>();
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var userId))
            {
                favoriteFoodIds = (await _context.Favorites
                    .Where(fav => fav.UserId == userId)
                    .Select(fav => fav.FoodId)
                    .ToListAsync())
                    .ToHashSet();
            }
        }

        var model = new FoodIndexViewModel
        {
            Foods = foods,
            Categories = categories,
            Search = search,
            CategoryId = categoryId,
            MinCal = minCal,
            MaxCal = maxCal,
            IsVegetarian = isVegetarian,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems,
            FavoriteFoodIds = favoriteFoodIds
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var food = await _context.Foods
            .Include(f => f.Category)
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsHidden);

        if (food == null) return NotFound();

        bool isFavorite = false;
        bool hasAllergyWarning = false;
        string? allergensFound = null;
        string? userAllergies = null;
        decimal userDailyCalorieTarget = 2000m;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var userId))
            {
                // Kiểm tra yêu thích
                isFavorite = await _context.Favorites.AnyAsync(fav => fav.UserId == userId && fav.FoodId == id);

                // Kiểm tra dị ứng đối chiếu với hồ sơ
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null && !string.IsNullOrWhiteSpace(profile.Allergies))
                {
                    userAllergies = profile.Allergies;
                    var matched = new FoodRestrictions(FoodRestrictions.Split(profile.Allergies)).MatchedAllergies(food);

                    if (matched.Any())
                    {
                        hasAllergyWarning = true;
                        allergensFound = string.Join(", ", matched);
                    }
                }

                var activeGoal = await _context.Goals.FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive);
                if (activeGoal != null && activeGoal.DailyCalorieTarget > 0)
                {
                    userDailyCalorieTarget = (await _budgets.GetAsync(userId)).TargetCalories;
                }
            }
        }

        var model = new FoodDetailsViewModel
        {
            Food = food,
            IsFavorite = isFavorite,
            HasAllergyWarning = hasAllergyWarning,
            AllergensFound = allergensFound,
            UserAllergies = userAllergies,
            UserDailyTargetCalories = userDailyCalorieTarget
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int foodId)
    {
        // Chống lỗi IDOR: Lấy userId trực tiếp từ Claims
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(fav => fav.UserId == userId && fav.FoodId == foodId);

        bool isFavorite;
        if (favorite != null)
        {
            _context.Favorites.Remove(favorite);
            isFavorite = false;
        }
        else
        {
            _context.Favorites.Add(new Favorite
            {
                UserId = userId,
                FoodId = foodId,
                CreatedAt = DateTime.UtcNow
            });
            isFavorite = true;
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, isFavorite = isFavorite });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Favorites()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var favoriteFoods = await _context.Favorites
            .Where(fav => fav.UserId == userId)
            .Include(fav => fav.Food)
                .ThenInclude(f => f.Category)
            .OrderByDescending(fav => fav.CreatedAt)
            .Select(fav => fav.Food)
            .ToListAsync();

        return View(favoriteFoods);
    }

    [HttpGet]
    public async Task<IActionResult> SearchJson(string? query, int? categoryId)
    {
        var q = _context.Foods
            .Where(f => !f.IsHidden);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var s = query.Trim();
            q = q.Where(f => f.Name.Contains(s) || (f.Ingredients != null && f.Ingredients.Contains(s)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            q = q.Where(f => f.CategoryId == categoryId.Value);
        }

        var items = await q
            .Take(25)
            .Select(f => new
            {
                id = f.Id,
                name = f.Name,
                category = f.Category.Name,
                servingName = f.ServingName,
                servingUnit = f.ServingName,
                servingSize = f.ServingSize,
                calories = f.Calories,
                protein = f.Protein,
                carb = f.Carb,
                carbs = f.Carb,
                fat = f.Fat,
                allergenNotes = f.AllergenNotes ?? ""
            })
            .ToListAsync();

        return Json(items);
    }
}
