using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình DbContext kết nối SQL Server
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
connectionString = await LocalDbDevelopmentConnection.ResolveAsync(connectionString, builder.Environment.IsDevelopment());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Cấu hình ASP.NET Core Identity với Primary Key int
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    // Quy tắc mật khẩu: Tối thiểu 8 ký tự, yêu cầu có chữ và số
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    // Cấu hình tài khoản & lockout
    options.User.RequireUniqueEmail = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. Cấu hình Cookie Authentication (Remember Me 14 ngày, Login/AccessDenied path)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Cấu hình Antiforgery Header để hỗ trợ AJAX Fetch JSON
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// 4. Đăng ký các Services hệ thống
builder.Services.AddHttpClient();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<INutritionCalculatorService, NutritionCalculatorService>();
builder.Services.AddScoped<GoalPlanningService>();
builder.Services.AddScoped<CalorieBudgetService>();
builder.Services.AddScoped<IWeightTrackingService, WeightTrackingService>();
builder.Services.AddScoped<AdaptiveCalorieService>();
builder.Services.AddScoped<IMealPlannerService, MealPlannerService>();
builder.Services.AddScoped<IChatSafetyFilter, ChatSafetyFilter>();
builder.Services.AddScoped<IUserContextBuilder, UserContextBuilder>();
builder.Services.AddScoped<IFoodToolService, FoodToolService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IGeminiChatService, GeminiChatService>();

// Add MVC Controllers & Views with JsonStringEnumConverter
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Route cho Areas (Admin)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// Route mặc định
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed Database tự động khi khởi động ứng dụng
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    // The local app may run under a different Windows identity from the CLI.
    // Upgrade the existing development database before any Goal query or seeding.
    // Only this reviewed additive migration is allowed automatically; an empty
    // database or any future migration still requires an explicit database update.
    if (app.Environment.IsDevelopment())
    {
        var db = services.GetRequiredService<AppDbContext>();
        const string adaptiveMigration = "20261107105859_AddAdaptiveCaloriePlan";
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Length == 1 && pending[0] == adaptiveMigration)
        {
            await db.Database.MigrateAsync();
        }
        else if (pending.Length > 0)
        {
            throw new InvalidOperationException(
                "Database còn migration chưa được áp dụng. Hãy chạy dotnet ef database update trước khi khởi động; chỉ migration bổ sung CaloriePlanJson được tự áp dụng trong Development.");
        }
    }
    try
    {
        await DbSeeder.SeedAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Đã xảy ra lỗi trong quá trình khởi tạo dữ liệu mẫu (DbSeeder).");
    }
}

app.Run();
