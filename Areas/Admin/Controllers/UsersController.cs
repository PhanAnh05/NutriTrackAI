using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Areas.Admin.Models;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public UsersController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string? roleFilter, string? statusFilter)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(term) || (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(roleFilter) && roleFilter != "all")
        {
            query = query.Where(u => u.RoleName == roleFilter);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all")
        {
            if (statusFilter == "locked") query = query.Where(u => u.IsLocked);
            else if (statusFilter == "active") query = query.Where(u => !u.IsLocked);
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
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

        var model = new AdminUserListViewModel
        {
            Users = users,
            SearchTerm = searchTerm,
            RoleFilter = roleFilter,
            StatusFilter = statusFilter
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int userId)
    {
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == userId)
        {
            TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản của chính mình.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }

        user.IsLocked = !user.IsLocked;
        if (user.IsLocked)
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        }
        else
        {
            user.LockoutEnd = null;
        }

        await _userManager.UpdateAsync(user);
        TempData["SuccessMessage"] = user.IsLocked
            ? $"Đã khóa tài khoản của '{user.FullName}' ({user.Email})."
            : $"Đã mở khóa tài khoản cho '{user.FullName}' ({user.Email}).";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int userId, string newRole)
    {
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == userId)
        {
            TempData["ErrorMessage"] = "Bạn không thể tự thay đổi vai trò của chính mình.";
            return RedirectToAction(nameof(Index));
        }

        if (newRole != "Admin" && newRole != "User")
        {
            TempData["ErrorMessage"] = "Vai trò không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
            return RedirectToAction(nameof(Index));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, newRole);

        user.RoleName = newRole;
        await _userManager.UpdateAsync(user);

        TempData["SuccessMessage"] = $"Đã cập nhật vai trò của '{user.FullName}' thành '{newRole}'.";
        return RedirectToAction(nameof(Index));
    }
}
