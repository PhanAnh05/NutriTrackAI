using System.ComponentModel.DataAnnotations;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.ViewModels;

public class ChatIndexViewModel
{
    // Danh sách các phiên trò chuyện của User
    public List<ChatSessionDto> Sessions { get; set; } = new();

    // Phiên trò chuyện hiện tại đang mở
    public int? ActiveSessionId { get; set; }
    public string ActiveSessionTitle { get; set; } = "Cuộc trò chuyện mới";

    // Danh sách tin nhắn trong phiên hiện tại
    public List<ChatMessageDto> Messages { get; set; } = new();

    // Các câu hỏi gợi ý nhanh chuẩn bị cho bảo vệ đồ án
    public List<string> QuickPrompts { get; set; } = new()
    {
        "Hôm nay tôi còn ăn được bao nhiêu calo?",
        "Gợi ý bữa tối dưới 500 kcal",
        "Tôi có thể thay thịt gà bằng món gì?",
        "Thực đơn hôm nay của tôi đủ protein chưa?",
        "Người giảm cân có nên uống trà sữa không?",
        "Nguyên tắc thâm hụt calo là gì?"
    };
}

public class ChatSessionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SendMessageInputModel
{
    public int? SessionId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tin nhắn.")]
    [StringLength(2000, ErrorMessage = "Tin nhắn không được vượt quá 2.000 ký tự.")]
    public string Message { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    public int SessionId { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string BotResponse { get; set; } = string.Empty;
    public bool IsMedicalRisk { get; set; }
    public string? MatchedRiskCategory { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
