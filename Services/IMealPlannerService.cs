using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Services;

public interface IMealPlannerService
{
    /// <summary>
    /// Thuật toán sinh thực đơn cá nhân hóa 3 hoặc 4 bữa, lọc dị ứng & món không thích, căn chỉnh calo ±10%
    /// </summary>
    Task<MealPlanResult> GeneratePlanAsync(int userId, int? customTargetCalories = null, int numberOfMeals = 4);

    /// <summary>
    /// Tìm kiếm danh sách món thay thế trong cùng CategoryId có độ lệch calo trong khoảng ±15%
    /// </summary>
    Task<List<FoodSwapCandidate>> GetSwapCandidatesAsync(int foodId, decimal currentServings, int userId);

    /// <summary>
    /// Lưu thực đơn gợi ý vào bảng meal_plans và meal_plan_items
    /// </summary>
    Task<int> SavePlanAsync(int userId, string planName, MealPlanResult planResult);

    /// <summary>
    /// Áp dụng toàn bộ bữa ăn trong thực đơn vào bảng meal_logs và meal_log_details của ngày chỉ định (mặc định hôm nay)
    /// </summary>
    Task<bool> ApplyPlanToTodayAsync(int userId, MealPlanResult planResult, DateTime? targetDate = null);

    /// <summary>
    /// Lấy danh sách thực đơn mẫu công khai của Admin
    /// </summary>
    Task<List<MealPlanTemplateSummaryDto>> GetPublicTemplatesAsync();

    /// <summary>
    /// Lấy danh sách thực đơn đã lưu của User
    /// </summary>
    Task<List<MealPlanTemplateSummaryDto>> GetUserSavedPlansAsync(int userId);

    /// <summary>
    /// Xem chi tiết một thực đơn đã lưu (theo Id)
    /// </summary>
    Task<MealPlanResult?> GetPlanByIdAsync(int planId, int userId);
}
