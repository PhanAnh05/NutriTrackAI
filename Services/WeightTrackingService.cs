using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;

namespace NutriTrackAI.Services;

public interface IWeightTrackingService
{
    Task<WeightLog?> GetLatestAsync(int userId, DateTime? throughDate = null, int? excludeId = null);
    Task<WeightLog> RecordWeightAsync(UserProfile profile, DateTime date, decimal weightKg, string? note,
        string? photoUrl = null, WeightLog? editedLog = null);
    Task DeleteAsync(UserProfile profile, WeightLog log);
    void UseLatestWeight(UserProfile profile, WeightLog latest);
}

// Callers hold a transaction across the log, profile and calorie budget updates.
public class WeightTrackingService(AppDbContext db) : IWeightTrackingService
{
    public Task<WeightLog?> GetLatestAsync(int userId, DateTime? throughDate = null, int? excludeId = null)
    {
        var logs = db.WeightLogs.Where(log => log.UserId == userId);
        if (throughDate is DateTime date)
            logs = logs.Where(log => log.LoggedOn < date.Date.AddDays(1));
        if (excludeId is int id)
            logs = logs.Where(log => log.Id != id);

        return logs.OrderByDescending(log => log.LoggedOn).ThenByDescending(log => log.Id).FirstOrDefaultAsync();
    }

    public async Task<WeightLog> RecordWeightAsync(UserProfile profile, DateTime date, decimal weightKg, string? note,
        string? photoUrl = null, WeightLog? editedLog = null)
    {
        var previousLatest = await GetLatestAsync(profile.UserId);
        var day = date.Date;
        var log = editedLog ?? await db.WeightLogs
            .Where(entry => entry.UserId == profile.UserId && entry.LoggedOn >= day && entry.LoggedOn < day.AddDays(1))
            .OrderByDescending(entry => entry.LoggedOn).ThenByDescending(entry => entry.Id)
            .FirstOrDefaultAsync();
        if (log == null)
        {
            log = new WeightLog { UserId = profile.UserId, CreatedAt = DateTime.UtcNow };
            db.WeightLogs.Add(log);
        }

        log.LoggedOn = day;
        log.WeightKg = weightKg;
        log.Note = note?.Trim();
        if (photoUrl != null) log.PhotoUrl = photoUrl;
        await db.SaveChangesAsync();

        var latest = await GetLatestAsync(profile.UserId);
        if (latest != null && (latest.Id == log.Id || previousLatest?.Id == log.Id))
            UseLatestWeight(profile, latest);

        return log;
    }

    public async Task DeleteAsync(UserProfile profile, WeightLog log)
    {
        var previousLatest = await GetLatestAsync(profile.UserId);
        db.WeightLogs.Remove(log);
        await db.SaveChangesAsync();

        if (previousLatest?.Id == log.Id && await GetLatestAsync(profile.UserId) is { } latest)
            UseLatestWeight(profile, latest);
    }

    public void UseLatestWeight(UserProfile profile, WeightLog latest)
    {
        profile.CurrentWeightKg = latest.WeightKg;
        profile.UpdatedAt = DateTime.UtcNow;
    }

    public static bool SameWeight(decimal firstKg, decimal secondKg) =>
        Math.Round(firstKg, 1, MidpointRounding.AwayFromZero) == Math.Round(secondKg, 1, MidpointRounding.AwayFromZero);

    public static string? LargeChangeWarning(WeightLog? reference, DateTime date, decimal weightKg)
    {
        if (reference == null) return null;

        var days = (date.Date - reference.LoggedOn.Date).Days;
        var difference = Math.Abs(weightKg - reference.WeightKg);
        var threshold = Math.Max(GoalPolicy.LargeWeightChangeKg, reference.WeightKg * GoalPolicy.LargeWeightChangePercent);
        return days is >= 0 and <= GoalPolicy.LargeWeightChangeWindowDays && difference > threshold
            ? $"Cân nặng thay đổi {difference:0.0} kg so với lần ghi tham chiếu ({reference.LoggedOn:dd/MM/yyyy}). Hãy tích xác nhận nếu số này đúng."
            : null;
    }
}
