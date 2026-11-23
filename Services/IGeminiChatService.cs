using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Services;

public interface IGeminiChatService
{
    /// <summary>
    /// Điều phối luồng xử lý tin nhắn: SafetyFilter -> UserContext -> ToolGrounding -> RAG -> Gemini API (với Smart Fallback) -> Lưu lịch sử
    /// </summary>
    Task<ChatResponseDto> SendMessageAsync(int userId, int? sessionId, string userMessage);

    /// <summary>
    /// Lấy danh sách các phiên trò chuyện của người dùng (chưa xóa mềm)
    /// </summary>
    Task<List<ChatSessionDto>> GetUserSessionsAsync(int userId);

    /// <summary>
    /// Lấy danh sách tin nhắn của một phiên trò chuyện cụ thể (có kiểm tra quyền sở hữu)
    /// </summary>
    Task<List<ChatMessageDto>> GetSessionMessagesAsync(int userId, int sessionId);

    /// <summary>
    /// Tạo mới một phiên trò chuyện trống
    /// </summary>
    Task<ChatSessionDto> CreateNewSessionAsync(int userId, string title = "Cuộc trò chuyện mới");

    /// <summary>
    /// Xóa mềm một phiên trò chuyện (IsDeleted = true)
    /// </summary>
    Task<bool> DeleteSessionAsync(int userId, int sessionId);

    /// <summary>
    /// Xóa toàn bộ lịch sử trò chuyện của người dùng
    /// </summary>
    Task<bool> ClearAllHistoryAsync(int userId);
}
