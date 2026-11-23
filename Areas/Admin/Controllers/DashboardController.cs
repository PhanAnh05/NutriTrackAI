using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Services;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var today = GoalPolicy.Today;
        var todayStartUtc = GoalPolicy.VietnamDateStartUtc(today);
        var monthStartUtc = GoalPolicy.VietnamDateStartUtc(new DateTime(today.Year, today.Month, 1));

        // 1. Thống kê người dùng
        var totalUsers = await _context.Users.CountAsync();
        var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= monthStartUtc);
        var newUsersToday = await _context.Users.CountAsync(u => u.CreatedAt >= todayStartUtc);

        // 2. Thống kê Chatbot AI
        var totalSessions = await _context.ChatSessions.CountAsync();
        var totalMessages = await _context.ChatMessages.CountAsync();

        // 3. Thống kê dữ liệu dinh dưỡng
        var totalFoods = await _context.Foods.CountAsync();
        var totalCategories = await _context.FoodCategories.CountAsync();
        var totalMealLogs = await _context.MealLogDetails.CountAsync();

        // 4. Thống kê loại mục tiêu
        var loseCount = await _context.Goals.CountAsync(g => g.GoalType == GoalType.Lose);
        var gainCount = await _context.Goals.CountAsync(g => g.GoalType == GoalType.Gain);
        var maintainCount = await _context.Goals.CountAsync(g => g.GoalType == GoalType.Maintain);

        // 5. Top 10 món ăn được ghi nhận nhiều nhất
        var topFoodsRaw = await _context.MealLogDetails
            .GroupBy(d => d.FoodId)
            .Select(g => new
            {
                FoodId = g.Key,
                Count = g.Count(),
                TotalCalories = g.Sum(d => d.Calories)
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var topFoodIds = topFoodsRaw.Select(x => x.FoodId).ToList();
        var foodDict = await _context.Foods
            .Include(f => f.Category)
            .Where(f => topFoodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f);

        var topFoods = topFoodsRaw
            .Where(x => foodDict.ContainsKey(x.FoodId))
            .Select(x => new TopFoodDto
            {
                FoodId = x.FoodId,
                FoodName = foodDict[x.FoodId].Name,
                CategoryName = foodDict[x.FoodId].Category?.Name ?? "Món ăn",
                LogCount = x.Count,
                TotalCaloriesLogged = x.TotalCalories
            })
            .ToList();

        // 6. Danh sách 5 người dùng đăng ký mới nhất
        var recentUsers = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new AdminUserItemDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                RoleName = u.RoleName,
                IsLocked = u.IsLocked,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            NewUsersThisMonth = newUsersThisMonth,
            NewUsersToday = newUsersToday,
            TotalChatSessions = totalSessions,
            TotalChatMessages = totalMessages,
            TotalFoods = totalFoods,
            TotalCategories = totalCategories,
            TotalMealLogsLogged = totalMealLogs,
            GoalTypeLoseCount = loseCount,
            GoalTypeGainCount = gainCount,
            GoalTypeMaintainCount = maintainCount,
            TopFoods = topFoods,
            RecentUsers = recentUsers
        };

        return View(model);
    }
}
