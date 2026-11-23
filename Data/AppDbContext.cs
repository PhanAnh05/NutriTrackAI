using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<WeightLog> WeightLogs => Set<WeightLog>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<FoodCategory> FoodCategories => Set<FoodCategory>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<MealLog> MealLogs => Set<MealLog>();
    public DbSet<MealLogDetail> MealLogDetails => Set<MealLogDetail>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<MealPlanItem> MealPlanItems => Set<MealPlanItem>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ExerciseLog> ExerciseLogs => Set<ExerciseLog>();
    public DbSet<WaterLog> WaterLogs => Set<WaterLog>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 1. Đổi tên bảng Identity cho gọn và đồng bộ với bảng users
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // 2. Quan hệ 1-1: ApplicationUser <-> UserProfile
        builder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(p => p.UserId);
            entity.Property(p => p.HeightCm).HasPrecision(5, 2);
            entity.Property(p => p.CurrentWeightKg).HasPrecision(5, 1);
            entity.Property(p => p.TargetWeightKg).HasPrecision(5, 1);
            entity.Property(p => p.MedicalConditions).HasDefaultValue(MedicalConditions.None);
            entity.Property(p => p.DoctorAwareOfPlan).HasDefaultValue(false);

            entity.HasOne(p => p.User)
                  .WithOne(u => u.Profile)
                  .HasForeignKey<UserProfile>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. WeightLog: Index (UserId, LoggedOn) để vẽ biểu đồ và tra cứu nhanh
        builder.Entity<WeightLog>(entity =>
        {
            entity.Property(w => w.WeightKg).HasPrecision(5, 1);
            entity.HasIndex(w => new { w.UserId, w.LoggedOn });

            entity.HasOne(w => w.User)
                  .WithMany(u => u.WeightLogs)
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 4. Goal: Index (UserId, IsActive)
        builder.Entity<Goal>(entity =>
        {
            entity.Property(g => g.StartWeight).HasPrecision(5, 1);
            entity.Property(g => g.TargetWeight).HasPrecision(5, 1);
            entity.Property(g => g.WeeklyRateKg).HasPrecision(4, 2);
            entity.HasIndex(g => new { g.UserId, g.IsActive });

            entity.HasOne(g => g.User)
                  .WithMany(u => u.Goals)
                  .HasForeignKey(g => g.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 5. FoodCategory & Food
        builder.Entity<FoodCategory>(entity =>
        {
            entity.HasIndex(c => c.Slug).IsUnique();
        });

        builder.Entity<Food>(entity =>
        {
            entity.Property(f => f.ServingSize).HasPrecision(8, 2);
            entity.Property(f => f.Calories).HasPrecision(8, 2);
            entity.Property(f => f.Protein).HasPrecision(8, 2);
            entity.Property(f => f.Carb).HasPrecision(8, 2);
            entity.Property(f => f.Fat).HasPrecision(8, 2);
            entity.Property(f => f.Fiber).HasPrecision(8, 2);
            entity.Property(f => f.Sugar).HasPrecision(8, 2);
            entity.Property(f => f.Sodium).HasPrecision(8, 2);

            entity.HasIndex(f => f.Name);
            entity.HasIndex(f => new { f.CategoryId, f.IsHidden });

            entity.HasOne(f => f.Category)
                  .WithMany(c => c.Foods)
                  .HasForeignKey(f => f.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. MealLog & MealLogDetail: Index (UserId, LoggedOn)
        builder.Entity<MealLog>(entity =>
        {
            entity.HasIndex(m => new { m.UserId, m.LoggedOn });

            entity.HasOne(m => m.User)
                  .WithMany(u => u.MealLogs)
                  .HasForeignKey(m => m.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MealLogDetail>(entity =>
        {
            entity.Property(d => d.Servings).HasPrecision(8, 2);
            entity.Property(d => d.Calories).HasPrecision(8, 2);
            entity.Property(d => d.Protein).HasPrecision(8, 2);
            entity.Property(d => d.Carb).HasPrecision(8, 2);
            entity.Property(d => d.Fat).HasPrecision(8, 2);
            entity.Property(d => d.Fiber).HasPrecision(8, 2);
            entity.Property(d => d.Sugar).HasPrecision(8, 2);
            entity.Property(d => d.Sodium).HasPrecision(8, 2);

            entity.HasOne(d => d.MealLog)
                  .WithMany(m => m.Details)
                  .HasForeignKey(d => d.MealLogId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Food)
                  .WithMany(f => f.MealLogDetails)
                  .HasForeignKey(d => d.FoodId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. MealPlan & MealPlanItem
        builder.Entity<MealPlan>(entity =>
        {
            entity.HasIndex(mp => new { mp.UserId, mp.IsTemplate });

            entity.HasOne(mp => mp.User)
                  .WithMany(u => u.MealPlans)
                  .HasForeignKey(mp => mp.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MealPlanItem>(entity =>
        {
            entity.Property(mpi => mpi.Servings).HasPrecision(8, 2);

            entity.HasOne(mpi => mpi.MealPlan)
                  .WithMany(mp => mp.Items)
                  .HasForeignKey(mpi => mpi.MealPlanId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(mpi => mpi.Food)
                  .WithMany(f => f.MealPlanItems)
                  .HasForeignKey(mpi => mpi.FoodId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. Exercise & ExerciseLog: Index (UserId, LoggedOn)
        builder.Entity<Exercise>(entity =>
        {
            entity.Property(e => e.MetValue).HasPrecision(5, 2);
        });

        builder.Entity<ExerciseLog>(entity =>
        {
            entity.Property(el => el.CaloriesBurned).HasPrecision(8, 2);
            entity.HasIndex(el => new { el.UserId, el.LoggedOn });

            entity.HasOne(el => el.User)
                  .WithMany(u => u.ExerciseLogs)
                  .HasForeignKey(el => el.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(el => el.Exercise)
                  .WithMany(e => e.ExerciseLogs)
                  .HasForeignKey(el => el.ExerciseId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 9. WaterLog: Index (UserId, LoggedOn)
        builder.Entity<WaterLog>(entity =>
        {
            entity.HasIndex(wl => new { wl.UserId, wl.LoggedOn });

            entity.HasOne(wl => wl.User)
                  .WithMany(u => u.WaterLogs)
                  .HasForeignKey(wl => wl.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 10. Favorite: Unique constraint (UserId, FoodId)
        builder.Entity<Favorite>(entity =>
        {
            entity.HasIndex(fav => new { fav.UserId, fav.FoodId }).IsUnique();

            entity.HasOne(fav => fav.User)
                  .WithMany(u => u.Favorites)
                  .HasForeignKey(fav => fav.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(fav => fav.Food)
                  .WithMany(f => f.Favorites)
                  .HasForeignKey(fav => fav.FoodId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 11. ChatSession & ChatMessage
        builder.Entity<ChatSession>(entity =>
        {
            entity.HasIndex(cs => new { cs.UserId, cs.IsDeleted, cs.UpdatedAt });

            entity.HasOne(cs => cs.User)
                  .WithMany(u => u.ChatSessions)
                  .HasForeignKey(cs => cs.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(cm => new { cm.SessionId, cm.CreatedAt });

            entity.HasOne(cm => cm.Session)
                  .WithMany(cs => cs.Messages)
                  .HasForeignKey(cm => cm.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. Article
        builder.Entity<Article>(entity =>
        {
            entity.HasIndex(a => new { a.Type, a.IsPublished });
        });
    }
}
