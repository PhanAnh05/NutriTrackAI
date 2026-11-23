using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NutriTrackAI.Services;

public class ChatSafetyFilter : IChatSafetyFilter
{
    private static readonly Dictionary<string, string[]> RiskCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Triệu chứng cấp tính nguy hiểm"] = new[]
        {
            "đau ngực", "tức ngực", "khó thở", "thở dốc", "ngất", "ngất xỉu",
            "choáng váng", "mất ý thức", "hôn mê", "nôn ra máu", "co giật"
        },
        ["Rối loạn ăn uống & Nhịn ăn cực đoan"] = new[]
        {
            "tự gây nôn", "móc họng", "rối loạn ăn uống", "chán ăn tâm thần", "bulimia",
            "anorexia", "nhịn ăn cực đoan", "nhịn ăn 800", "nhịn ăn dưới 800", "nhịn đói 3 ngày",
            "nhịn đói 7 ngày", "nhịn đói cả tuần", "nhịn ăn hoàn toàn"
        },
        ["Thuốc kê đơn & Thuốc giảm cân"] = new[]
        {
            "thuốc giảm cân", "thuốc kê đơn", "liều thuốc", "uống thuốc gì", "kê đơn",
            "tiêm insulin", "insulin", "metformin", "thuốc hạ đường huyết", "thuốc lợi tiểu",
            "đơn thuốc", "uống thuốc liều bao nhiêu"
        },
        ["Bệnh lý nghiêm trọng"] = new[]
        {
            "tiểu đường type 1", "tiểu đường týp 1", "suy thận", "chạy thận", "lọc máu",
            "suy tim", "xơ gan", "ung thư", "xuất huyết dạ dày"
        },
        ["Thai kỳ & Sau sinh"] = new[]
        {
            "đang mang thai", "có bầu", "thai kỳ", "mang bầu", "mẹ bầu", "cho con bú"
        },
        ["Dị ứng nặng & Phản vệ"] = new[]
        {
            "dị ứng phản vệ", "sốc phản vệ", "phù thanh quản", "sưng họng khó thở"
        },
        ["Sụt cân bất thường"] = new[]
        {
            "sụt cân không rõ nguyên nhân", "sụt cân đột ngột", "sụt cân quá nhanh không rõ lý do"
        }
    };

    private static readonly string[] NutritionKeywords = new[]
    {
        "calo", "calorie", "protein", "đạm", "carb", "tinh bột", "fat", "chất béo",
        "hôm nay", "còn lại", "đã ăn", "món", "thực đơn", "nước", "uống nước",
        "tập luyện", "vận động", "gợi ý", "thay thế", "bữa sáng", "bữa trưa", "bữa tối", "bữa phụ"
    };

    public const string StandardRefusalMessage =
        "⚠️ CẢNH BÁO Y TẾ: Hệ thống không chẩn đoán, không kê thuốc và không điều trị bệnh. " +
        "Với các triệu chứng sức khỏe, thuốc kê đơn hoặc bệnh lý cụ thể, bạn cần trao đổi trực tiếp với bác sĩ " +
        "hoặc chuyên gia dinh dưỡng lâm sàng. NutriTrackAI chỉ hỗ trợ theo dõi ăn uống và cân nặng mang tính tham khảo lối sống.";

    public ChatSafetyResult CheckSafety(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new ChatSafetyResult { IsMedicalRisk = false };
        }

        var normalizedMessage = userMessage.Trim().ToLowerInvariant();
        var unaccentedMessage = RemoveDiacritics(normalizedMessage);
        var matchedKeywords = new List<string>();
        string? matchedCategory = null;

        foreach (var category in RiskCategories)
        {
            foreach (var keyword in category.Value)
            {
                var unaccentedKeyword = RemoveDiacritics(keyword);
                if (normalizedMessage.Contains(keyword) || unaccentedMessage.Contains(unaccentedKeyword))
                {
                    matchedKeywords.Add(keyword);
                    matchedCategory ??= category.Key;
                }
            }
        }

        var isRisk = matchedKeywords.Any();
        var result = new ChatSafetyResult
        {
            IsMedicalRisk = isRisk,
            MatchedRiskCategory = matchedCategory,
            MatchedKeywords = matchedKeywords.Distinct().ToList(),
            WarningMessage = isRisk ? StandardRefusalMessage : string.Empty
        };

        if (isRisk)
        {
            // Kiểm tra xem câu hỏi có chứa phần hỏi calo/dinh dưỡng thông thường không
            var hasNutrition = NutritionKeywords.Any(k => 
                normalizedMessage.Contains(k) || unaccentedMessage.Contains(RemoveDiacritics(k)));
            result.HasSafeNutritionPart = hasNutrition;
            if (hasNutrition)
            {
                result.SafeNutritionQuery = userMessage;
            }
        }

        return result;
    }

    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd').Replace('Đ', 'D');
    }
}
