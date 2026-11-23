namespace NutriTrackAI.Models.Enums;

public enum DietType
{
    Normal = 1,         // Bình thường
    Vegetarian = 2,     // Ăn chay
    LowCarb = 3,        // Giảm tinh bột
    HighProtein = 4     // Giàu đạm
}

public static class DietTypeExtensions
{
    public static string GetVietnameseLabel(this DietType diet) => diet switch
    {
        DietType.Normal => "Cân bằng",
        DietType.Vegetarian => "Ăn chay",
        DietType.LowCarb => "Giảm tinh bột",
        DietType.HighProtein => "Giàu đạm",
        _ => "Chưa xác định"
    };
}
