using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;

namespace NutriTrackAI.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireOnboardingAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        // Nếu chưa đăng nhập, bỏ qua (để Authorize filter xử lý nếu có)
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        // Bỏ qua nếu là Admin
        if (user.IsInRole("Admin"))
        {
            await next();
            return;
        }

        // Kiểm tra controller hiện tại để tránh loop
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        if (string.Equals(controllerName, "Onboarding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out var userId))
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var isCompleted = await dbContext.UserProfiles
                .Where(p => p.UserId == userId)
                .Select(p => p.OnboardingCompleted)
                .FirstOrDefaultAsync();

            if (!isCompleted)
            {
                context.Result = new RedirectToActionResult("Index", "Onboarding", null);
                return;
            }
        }

        await next();
    }
}
