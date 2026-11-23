namespace NutriTrackAI.Services;

public interface IChatSafetyFilter
{
    /// <summary>
    /// Kiểm tra câu hỏi của người dùng trước khi gọi LLM
    /// </summary>
    ChatSafetyResult CheckSafety(string userMessage);
}

public class ChatSafetyResult
{
    /// <summary>
    /// Cờ cảnh báo phát hiện rủi ro y tế / bệnh lý / thuốc / triệu chứng cấp tính
    /// </summary>
    public bool IsMedicalRisk { get; set; }

    /// <summary>
    /// Nhóm rủi ro y tế được nhận diện (Triệu chứng, Thuốc, Bệnh lý mãn tính, Rối loạn ăn uống, v.v.)
    /// </summary>
    public string? MatchedRiskCategory { get; set; }

    /// <summary>
    /// Danh sách các từ khóa rủi ro phát hiện trong câu hỏi
    /// </summary>
    public List<string> MatchedKeywords { get; set; } = new();

    /// <summary>
    /// Thông điệp từ chối/cảnh báo y tế chuẩn bắt buộc trả về
    /// </summary>
    public string WarningMessage { get; set; } = string.Empty;

    /// <summary>
    /// Cho biết câu hỏi có chứa phần hỏi dữ liệu dinh dưỡng/calo an toàn hay không
    /// </summary>
    public bool HasSafeNutritionPart { get; set; }

    /// <summary>
    /// Phần câu hỏi dinh dưỡng đã được tách an toàn (nếu có)
    /// </summary>
    public string SafeNutritionQuery { get; set; } = string.Empty;
}
