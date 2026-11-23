using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext context,
        IEmailSender emailSender,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
    }

    // ==========================================
    // 1. REGISTER
    // ==========================================
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Kiểm tra trùng Email
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Địa chỉ email này đã được sử dụng. Vui lòng đăng nhập hoặc sử dụng email khác.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName.Trim(),
            RoleName = "User",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            // Gán role User mặc định
            await _userManager.AddToRoleAsync(user, "User");

            // Khởi tạo bản ghi UserProfile sơ bộ
            var profile = new UserProfile
            {
                UserId = user.Id,
                Gender = model.Gender,
                DateOfBirth = model.DateOfBirth,
                HeightCm = 170m,
                CurrentWeightKg = 60m,
                TargetWeightKg = 60m,
                DailyWaterGoalMl = 2000,
                OnboardingCompleted = false,
                UpdatedAt = DateTime.UtcNow
            };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Người dùng mới đăng ký thành công: {Email}", user.Email);

            // Đăng nhập tự động
            await _signInManager.SignInAsync(user, isPersistent: false);

            // Chuyển hướng sang bước khảo sát Onboarding
            return RedirectToAction("Index", "Onboarding");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    // ==========================================
    // 2. LOGIN
    // ==========================================
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAppropriatePage();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            return View(model);
        }

        // Chặn người dùng nếu bị khóa thủ công hoặc khóa hệ thống
        if (user.IsLocked || await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Đăng nhập bị từ chối do tài khoản bị khóa: {Email}", user.Email);
            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Quản trị viên để được hỗ trợ.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Cập nhật thời điểm đăng nhập gần nhất
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Người dùng đăng nhập thành công: {Email}", user.Email);

            // Điều hướng theo Role & Onboarding Status
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return LocalRedirect(model.ReturnUrl);
                }
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null || !profile.OnboardingCompleted)
            {
                return RedirectToAction("Index", "Onboarding");
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Tài khoản {Email} bị khóa tạm thời do nhập sai quá số lần quy định.", user.Email);
            ModelState.AddModelError(string.Empty, "Tài khoản đã bị tạm khóa do nhập sai mật khẩu quá 5 lần. Vui lòng thử lại sau 15 phút.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
        return View(model);
    }

    // ==========================================
    // 3. FORGOT PASSWORD & RESET PASSWORD
    // ==========================================
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && !user.IsLocked)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { token, email = user.Email },
                protocol: Request.Scheme);

            var emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #10b981; margin-bottom: 20px;'>NutriTrackAI - Yêu cầu đặt lại mật khẩu</h2>
                    <p>Xin chào <strong>{user.FullName}</strong>,</p>
                    <p>Hệ thống nhận được yêu cầu đặt lại mật khẩu cho tài khoản liên kết với địa chỉ email này.</p>
                    <p style='margin: 25px 0;'>
                        <a href='{callbackUrl}' style='display: inline-block; background-color: #10b981; color: #ffffff; padding: 12px 24px; border-radius: 6px; text-decoration: none; font-weight: bold;'>Đặt lại mật khẩu ngay</a>
                    </p>
                    <p style='color: #64748b; font-size: 14px;'>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email. Đường dẫn có hiệu lực trong vòng 24 giờ.</p>
                    <hr style='border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;' />
                    <p style='font-size: 12px; color: #94a3b8;'>NutriTrackAI - Hệ thống hỗ trợ quản lý cân nặng và dinh dưỡng thông minh.</p>
                </div>";

            await _emailSender.SendEmailAsync(model.Email, "NutriTrackAI - Đặt lại mật khẩu tài khoản", emailBody);
        }

        // Luôn trả về thông báo trung tính chống lỗi User Enumeration
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token = null, string? email = null)
    {
        if (token == null || email == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var model = new ResetPasswordViewModel
        {
            Token = token,
            Email = email
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Không lộ thông tin email không tồn tại
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            _logger.LogInformation("Người dùng {Email} đã đặt lại mật khẩu thành công.", model.Email);
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    // ==========================================
    // 4. LOGOUT & ACCESS DENIED
    // ==========================================
    [HttpPost]
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Người dùng đã đăng xuất.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToAppropriatePage()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
        return RedirectToAction("Index", "Dashboard");
    }
}
