using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriTrackAI.Filters;
using NutriTrackAI.Models.ViewModels;
using NutriTrackAI.Services;

namespace NutriTrackAI.Controllers;

[Authorize]
[RequireOnboarding]
public class ChatController : Controller
{
    private readonly IGeminiChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IGeminiChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? sessionId)
    {
        var userId = GetCurrentUserId();
        var sessions = await _chatService.GetUserSessionsAsync(userId);

        int? activeSessionId = sessionId;
        string activeSessionTitle = "Cuộc trò chuyện mới";
        var messages = new List<ChatMessageDto>();

        if (activeSessionId.HasValue)
        {
            var matched = sessions.FirstOrDefault(s => s.Id == activeSessionId.Value);
            if (matched != null)
            {
                activeSessionTitle = matched.Title;
                messages = await _chatService.GetSessionMessagesAsync(userId, activeSessionId.Value);
            }
            else
            {
                activeSessionId = null;
            }
        }
        else if (sessions.Any())
        {
            var firstSession = sessions.First();
            activeSessionId = firstSession.Id;
            activeSessionTitle = firstSession.Title;
            messages = await _chatService.GetSessionMessagesAsync(userId, firstSession.Id);
        }

        var model = new ChatIndexViewModel
        {
            Sessions = sessions,
            ActiveSessionId = activeSessionId,
            ActiveSessionTitle = activeSessionTitle,
            Messages = messages
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageInputModel input)
    {
        var userId = GetCurrentUserId();

        if (input == null || string.IsNullOrWhiteSpace(input.Message))
        {
            return BadRequest(new { success = false, message = "Nội dung tin nhắn không được để trống." });
        }

        try
        {
            var response = await _chatService.SendMessageAsync(userId, input.SessionId, input.Message);
            return Ok(new
            {
                success = true,
                sessionId = response.SessionId,
                sessionTitle = response.SessionTitle,
                userMessage = response.UserMessage,
                botResponse = response.BotResponse,
                isMedicalRisk = response.IsMedicalRisk,
                timestamp = response.Timestamp.ToString("HH:mm")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi tin nhắn cho User {UserId}", userId);
            return StatusCode(500, new { success = false, message = "Đã xảy ra sự cố khi kết nối với trợ lý AI. Vui lòng thử lại sau." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewSession()
    {
        var userId = GetCurrentUserId();
        var newSession = await _chatService.CreateNewSessionAsync(userId);
        return RedirectToAction(nameof(Index), new { sessionId = newSession.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSession(int sessionId)
    {
        var userId = GetCurrentUserId();
        await _chatService.DeleteSessionAsync(userId, sessionId);
        TempData["SuccessMessage"] = "Đã xóa phiên trò chuyện.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllHistory()
    {
        var userId = GetCurrentUserId();
        await _chatService.ClearAllHistoryAsync(userId);
        TempData["SuccessMessage"] = "Đã dọn sạch toàn bộ lịch sử trò chuyện.";
        return RedirectToAction(nameof(Index));
    }

    private int GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            throw new UnauthorizedAccessException("Người dùng chưa được xác thực hợp lệ.");
        }
        return userId;
    }
}
