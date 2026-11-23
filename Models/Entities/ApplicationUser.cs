using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 1: users - Tài khoản người dùng (kế thừa IdentityUser<int>)
/// </summary>
[Table("users")]
public class ApplicationUser : IdentityUser<int>
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string RoleName { get; set; } = "User"; // "User" hoặc "Admin"

    public bool IsLocked { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation Properties
    public virtual UserProfile? Profile { get; set; }
    public virtual ICollection<WeightLog> WeightLogs { get; set; } = new List<WeightLog>();
    public virtual ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public virtual ICollection<MealLog> MealLogs { get; set; } = new List<MealLog>();
    public virtual ICollection<MealPlan> MealPlans { get; set; } = new List<MealPlan>();
    public virtual ICollection<ExerciseLog> ExerciseLogs { get; set; } = new List<ExerciseLog>();
    public virtual ICollection<WaterLog> WaterLogs { get; set; } = new List<WaterLog>();
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public virtual ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
