using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;
using NutriTrackAI.Models.ViewModels;

namespace NutriTrackAI.Services;

public class GeminiChatService : IGeminiChatService
{
    private readonly AppDbContext _context;
    private readonly IChatSafetyFilter _safetyFilter;
    private readonly IUserContextBuilder _contextBuilder;
    private readonly IFoodToolService _foodTool;
    private readonly IRagService _ragService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiChatService> _logger;

    public GeminiChatService(
        AppDbContext context,
        IChatSafetyFilter safetyFilter,
        IUserContextBuilder contextBuilder,
        IFoodToolService foodTool,
        IRagService ragService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GeminiChatService> logger)
    {
        _context = context;
        _safetyFilter = safetyFilter;
        _contextBuilder = contextBuilder;
        _foodTool = foodTool;
        _ragService = ragService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ChatResponseDto> SendMessageAsync(int userId, int? sessionId, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new ArgumentException("Nội dung tin nhắn không được để trống.", nameof(userMessage));
        }

        userMessage = userMessage.Trim();

        // 1. Quản lý phiên trò chuyện (ChatSession)
        ChatSession? session = null;
        if (sessionId.HasValue && sessionId.Value > 0)
        {
            session = await _context.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.UserId == userId && !s.IsDeleted);
        }

        if (session == null)
        {
            session = new ChatSession
            {
                UserId = userId,
                Title = GenerateSessionTitle(userMessage),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        // 2. BƯỚC 1: Bộ lọc an toàn y tế (ChatSafetyFilter)
        var safetyResult = _safetyFilter.CheckSafety(userMessage);
        if (safetyResult.IsMedicalRisk && !safetyResult.HasSafeNutritionPart)
        {
            var warningResponse = AiPromptBuilder.WithMedicalDisclaimer(safetyResult.WarningMessage +
                "\n\n*Nếu bạn đang gặp phải các dấu hiệu nguy cấp như đau thắt ngực, khó thở, choáng ngất hoặc nghi ngờ phản vệ, hãy liên hệ ngay trung tâm cấp cứu 115 hoặc cơ sở y tế gần nhất.*");

            await SaveMessagesAsync(session.Id, userMessage, warningResponse);

            return new ChatResponseDto
            {
                SessionId = session.Id,
                SessionTitle = session.Title,
                UserMessage = userMessage,
                BotResponse = warningResponse,
                IsMedicalRisk = true,
                MatchedRiskCategory = safetyResult.MatchedRiskCategory
            };
        }

        // 3. BƯỚC 2: Thu thập Ngữ cảnh cá nhân + RAG + Tool Pre-routing
        var userContext = await _contextBuilder.BuildUserContextAsync(userId);
        if (userContext.PlanSuspended)
        {
            var trackingResponse = $"Ứng dụng hiện hỗ trợ theo dõi nhật ký. Hôm nay bạn đã ghi {userContext.CaloriesConsumedToday:N0} kcal và {userContext.TotalWaterTodayMl:N0} ml nước. Bạn có thể tra cứu món tại trang Thực phẩm và xem trạng thái kế hoạch tại Hồ sơ.";
            if (safetyResult.IsMedicalRisk) trackingResponse = safetyResult.WarningMessage + "\n\n" + trackingResponse;
            trackingResponse = AiPromptBuilder.WithMedicalDisclaimer(trackingResponse);
            await SaveMessagesAsync(session.Id, userMessage, trackingResponse);
            return new ChatResponseDto
            {
                SessionId = session.Id, SessionTitle = session.Title, UserMessage = userMessage,
                BotResponse = trackingResponse, IsMedicalRisk = safetyResult.IsMedicalRisk,
                MatchedRiskCategory = safetyResult.MatchedRiskCategory
            };
        }
        var systemContextText = UserContextBuilder.FormatSystemData(userContext);
        var ragContextText = await _ragService.GetRagContextAsync(userMessage);

        // Kích hoạt Pre-routing truy vấn CSDL thật dựa trên ý định
        var toolDataBlock = await ExecutePreRoutingToolsAsync(userId, userMessage, userContext);

        // 4. BƯỚC 3: Ghép System Prompt chặt chẽ theo Mục 16.1 GEMINI.md
        var systemPrompt = AiPromptBuilder.Build(userContext, systemContextText, toolDataBlock, ragContextText);

        // 5. BƯỚC 4: Gọi Gemini API với cơ chế Smart Fallback
        string botResponse;
        var apiKey = _configuration["Gemini:ApiKey"];
        var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";

        if (!string.IsNullOrWhiteSpace(apiKey) && apiKey != "YOUR_GEMINI_API_KEY")
        {
            botResponse = await CallGeminiApiAsync(apiKey, model, systemPrompt, session.Id, userMessage);
            if (string.IsNullOrWhiteSpace(botResponse))
            {
                // Fallback nếu API lỗi mạng hoặc hết quota
                botResponse = GenerateSmartFallbackResponse(userMessage, userContext, toolDataBlock);
            }
        }
        else
        {
            // Chạy Smart Fallback Engine sử dụng CSDL thật
            botResponse = GenerateSmartFallbackResponse(userMessage, userContext, toolDataBlock);
        }

        // Nếu câu hỏi có rủi ro y tế nhưng có kèm ý định calo an toàn, thêm cảnh báo ở đầu
        if (safetyResult.IsMedicalRisk && safetyResult.HasSafeNutritionPart)
        {
            botResponse = safetyResult.WarningMessage + "\n\n" + botResponse;
        }

        botResponse = AiPromptBuilder.WithMedicalDisclaimer(botResponse);

        // 6. BƯỚC 5: Lưu tin nhắn & Cập nhật phiên chat
        await SaveMessagesAsync(session.Id, userMessage, botResponse);

        return new ChatResponseDto
        {
            SessionId = session.Id,
            SessionTitle = session.Title,
            UserMessage = userMessage,
            BotResponse = botResponse,
            IsMedicalRisk = safetyResult.IsMedicalRisk,
            MatchedRiskCategory = safetyResult.MatchedRiskCategory
        };
    }

    public async Task<List<ChatSessionDto>> GetUserSessionsAsync(int userId)
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new ChatSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                MessageCount = s.Messages.Count
            })
            .ToListAsync();

        return sessions;
    }

    public async Task<List<ChatMessageDto>> GetSessionMessagesAsync(int userId, int sessionId)
    {
        // Chống lỗi IDOR: Kiểm tra quyền sở hữu phiên
        var session = await _context.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && !s.IsDeleted);

        if (session == null) return new List<ChatMessageDto>();

        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();

        return messages;
    }

    public async Task<ChatSessionDto> CreateNewSessionAsync(int userId, string title = "Cuộc trò chuyện mới")
    {
        var session = new ChatSession
        {
            UserId = userId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();

        return new ChatSessionDto
        {
            Id = session.Id,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            MessageCount = 0
        };
    }

    public async Task<bool> DeleteSessionAsync(int userId, int sessionId)
    {
        // Chống IDOR: Chỉ xóa khi s.UserId == userId
        var session = await _context.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

        if (session == null) return false;

        session.IsDeleted = true;
        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClearAllHistoryAsync(int userId)
    {
        var sessions = await _context.ChatSessions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .ToListAsync();

        foreach (var s in sessions)
        {
            s.IsDeleted = true;
            s.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    // ==========================================
    // CÁC HÀM TRỢ GIÚP NỘI BỘ
    // ==========================================

    private async Task<string> ExecutePreRoutingToolsAsync(int userId, string query, UserNutritionContext userCtx)
    {
        var q = query.ToLowerInvariant();
        var u = ChatSafetyFilter.RemoveDiacritics(q);
        var sb = new StringBuilder();

        // 1. Ý định hỏi Calo còn lại
        if (q.Contains("còn lại") || u.Contains("con lai") || q.Contains("còn ăn được") || u.Contains("con an duoc") || q.Contains("calo còn lại") || u.Contains("calo con lai") || q.Contains("ngân sách calo") || (u.Contains("hom nay") && u.Contains("calo")))
        {
            var remainingData = await _foodTool.GetRemainingCaloriesAsync(userId);
            sb.AppendLine("=== [DỮ LIỆU CSDL CHÍNH XÁC: CALO CÒN LẠI HÔM NAY] ===");
            sb.AppendLine($"- {userCtx.CalorieReferenceLabel}: {remainingData.DailyTarget:N0} kcal");
            sb.AppendLine($"- Đã nạp từ các bữa ăn: {remainingData.Consumed:N0} kcal");
            sb.AppendLine($"- Vận động đã ghi: {remainingData.Burned:N0} kcal; phần cộng lại: {remainingData.ExerciseCreditApplied:N0} kcal.");
            sb.AppendLine($"- {(userCtx.UsesMaintenanceReference ? "Chênh lệch so với mốc duy trì" : "Calo còn lại")}: {remainingData.Remaining:N0} kcal");
            sb.AppendLine();
        }

        // 2. Ý định Gợi ý bữa tối / bữa ăn dưới X kcal
        if (q.Contains("bữa tối") || u.Contains("bua toi") || q.Contains("gợi ý bữa") || u.Contains("goi y bua") || q.Contains("bữa trưa") || u.Contains("bua trua") || q.Contains("ăn gì") || u.Contains("an gi"))
        {
            var targetKcal = Math.Max(0m, Math.Min(500m, userCtx.RemainingCalories));
            var matchKcal = Regex.Match(u, @"duoi\s*(\d{3,4})");
            if (!matchKcal.Success) matchKcal = Regex.Match(q, @"dưới\s*(\d{3,4})");

            if (matchKcal.Success && decimal.TryParse(matchKcal.Groups[1].Value, out var parsedKcal))
            {
                targetKcal = Math.Min(parsedKcal, userCtx.RemainingCalories);
            }
            else if (userCtx.RemainingCalories > 150)
            {
                targetKcal = Math.Min(600, userCtx.RemainingCalories);
            }

            sb.AppendLine("### Gợi ý món theo dữ liệu danh mục");
            if (targetKcal > 0 && !userCtx.HasUnmappedAllergen)
            {
                var meal = await _foodTool.SuggestMealAsync(targetKcal, userCtx.DietType, userCtx.LocalAllergyTerms,
                    userCtx.Constraints.MaxSodiumMg.HasValue, userCtx.LocalDislikedFoods);
                if (meal.SuggestedItems.Count > 0)
                {
                    sb.AppendLine($"- Tổng năng lượng gợi ý: {meal.TotalKcal:N0} kcal");
                    foreach (var item in meal.SuggestedItems) AppendCatalogueItem(sb, item);
                }
                else sb.AppendLine("Chưa có món phù hợp trong dữ liệu danh mục.");
            }
            else sb.AppendLine("Chưa đủ dữ liệu để đề xuất bữa ăn theo ngân sách và ràng buộc hiện tại.");
            sb.AppendLine();
        }

        var mentionedFood = await _foodTool.FindMentionedFoodAsync(q);
        // 3. Ý định Thay thế món ăn / Đổi món
        var wantsSwap = Regex.IsMatch(u, @"\b(thay(?! doi)|doi mon)\b");
        if (wantsSwap && mentionedFood != null)
        {
            sb.AppendLine($"### Đổi món {mentionedFood}");
            if (!userCtx.HasUnmappedAllergen)
            {
                var swap = await _foodTool.GetSwapSuggestionsAsync(mentionedFood, userCtx.DietType, userCtx.LocalAllergyTerms,
                    userCtx.Constraints.MaxSodiumMg.HasValue, userCtx.LocalDislikedFoods);
                sb.AppendLine(swap.Note);
                foreach (var item in swap.AlternativeFoods) AppendCatalogueItem(sb, item);
            }
            else sb.AppendLine("Chưa đủ dữ liệu để xác định món thay thế theo ràng buộc hiện tại.");
            sb.AppendLine();
        }

        // 4. Ý định Kiểm tra Protein
        if (q.Contains("protein") || u.Contains("protein") || q.Contains("chất đạm") || u.Contains("chat dam") || q.Contains("đủ đạm") || u.Contains("du dam"))
        {
            var nutri = await _foodTool.GetTodayNutritionAsync(userId);
            sb.AppendLine("### Chất đạm hôm nay");
            sb.AppendLine($"- Đã nạp: {nutri.Protein:0.0} g");
            if (nutri.RecommendedMacros is { UnavailableReason: null } macros)
                sb.AppendLine($"- Mức tham khảo máy chủ tính: {macros.ProteinGrams:0.0} g/ngày");
            else if (nutri.RecommendedMacros?.UnavailableReason is { } reason)
                sb.AppendLine(reason);
            sb.AppendLine();
        }

        if (mentionedFood != null && !wantsSwap)
        {
            var foods = await _foodTool.SearchFoodsAsync(mentionedFood, excludeAllergens: userCtx.LocalAllergyTerms,
                vegetarianOnly: userCtx.DietType == DietType.Vegetarian, dislikedFoods: userCtx.LocalDislikedFoods);
            sb.AppendLine($"=== [TRA CỨU MÓN '{mentionedFood}'] ===");
            if (foods.Any())
            {
                foreach (var f in foods)
                {
                    AppendCatalogueItem(sb, f);
                }
            }
            else
            {
                sb.AppendLine("Chưa có kết quả phù hợp với bộ lọc của bạn.");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void AppendCatalogueItem(StringBuilder text, FoodSearchResultDto item)
    {
        var serving = item.CookingState == null ? item.ServingUnit : $"{item.ServingUnit}, {item.CookingState}";
        text.AppendLine($"- {item.Name} ({serving}): {item.Calories:N0} kcal, đạm {item.Protein:0.0} g, tinh bột {item.Carb:0.0} g, béo {item.Fat:0.0} g, xơ {item.Fiber:0.0} g, đường {item.Sugar:0.0} g, natri {item.Sodium:N0} mg");
        if (!string.IsNullOrWhiteSpace(item.Ingredients)) text.AppendLine($"  Thành phần: {item.Ingredients}");
        if (!string.IsNullOrWhiteSpace(item.AllergenNotes)) text.AppendLine($"  Nhãn dị ứng trong danh mục: {item.AllergenNotes}");
        if (!string.IsNullOrWhiteSpace(item.HealthWarning)) text.AppendLine($"  Lưu ý của danh mục: {item.HealthWarning}");
    }

    private async Task<string> CallGeminiApiAsync(string apiKey, string model, string systemPrompt, int sessionId, string userMessage)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            // Lấy 4 tin nhắn gần nhất để duy trì ngữ cảnh ngắn
            var recentMessages = await _context.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(4)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            var contentsList = new List<object>();

            // Older assistant messages may contain raw profile fields from the previous prompt.
            var lastLegacyReply = recentMessages.FindLastIndex(message => message.Role == ChatRole.Assistant &&
                !message.Content.EndsWith(GoalPolicy.MedicalDisclaimer, StringComparison.Ordinal));
            var includeTurn = false;
            foreach (var msg in recentMessages.Skip(lastLegacyReply + 1))
            {
                if (msg.Role == ChatRole.User) includeTurn = !_safetyFilter.CheckSafety(msg.Content).IsMedicalRisk;
                if (!includeTurn) continue;
                contentsList.Add(new
                {
                    role = msg.Role == ChatRole.User ? "user" : "model",
                    parts = new[] { new { text = msg.Content } }
                });
            }

            // Thêm tin nhắn hiện tại
            contentsList.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestBody = new
            {
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = contentsList,
                generationConfig = new
                {
                    temperature = 0.4,
                    maxOutputTokens = 1200
                }
            };

            var response = await client.PostAsJsonAsync(url, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API call failed with status {StatusCode}", response.StatusCode);
                return string.Empty;
            }

            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            if (jsonDoc != null && jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                {
                    return parts[0].GetProperty("text").GetString() ?? string.Empty;
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối Gemini API");
            return string.Empty;
        }
    }

    private static string GenerateSmartFallbackResponse(string query, UserNutritionContext context, string catalogueData)
    {
        var normalized = ChatSafetyFilter.RemoveDiacritics(query.ToLowerInvariant());
        if (new[] { "giai doan", "tdee", "thay doi", "giam nhanh", "som hon", "muc tieu" }.Any(normalized.Contains))
        {
            return $"{context.PlanSummary}\n{context.CalorieReferenceLabel}: {context.DailyCalorieTarget:N0} kcal/ngày. " +
                $"{CalorieBudget.MaintenanceLabel}: {context.Tdee:N0} kcal/ngày. Xem trang Mục tiêu để đánh giá và xác nhận. Chatbot không tự thay đổi kế hoạch.";
        }
        if (new[] { "nhin an", "nhin doi", "keto", "800 kcal" }.Any(normalized.Contains))
            return "Hãy tham khảo bác sĩ hoặc chuyên gia dinh dưỡng trước khi thay đổi chế độ ăn. Bạn có thể xem giới hạn hiện tại ở trang Mục tiêu.";

        if (!string.IsNullOrWhiteSpace(catalogueData))
        {
            var constraints = AiPromptBuilder.DescribeDiet(context.Constraints).ToArray();
            var guidance = constraints.Length > 0 ? $"\n\nRàng buộc cần đối chiếu: {string.Join("; ", constraints)}." : "";
            return $"{catalogueData.Trim()}{guidance}\n\nGợi ý mang tính tham khảo, hãy kiểm tra nhãn thực phẩm.";
        }

        return $"{context.CalorieReferenceLabel}: {context.DailyCalorieTarget:N0} kcal/ngày. " +
            $"Hôm nay bạn đã ghi {context.CaloriesConsumedToday:N0} kcal. " +
            "Tôi có thể giải thích số liệu từ nhật ký và danh mục. Nếu thiếu dữ liệu cho món bạn hỏi, hãy kiểm tra nhãn hoặc trang Thực phẩm.";
    }

    private async Task SaveMessagesAsync(int sessionId, string userMessage, string botResponse)
    {
        var userMsgEntity = new ChatMessage
        {
            SessionId = sessionId,
            Role = ChatRole.User,
            Content = userMessage,
            CreatedAt = DateTime.UtcNow
        };
        _context.ChatMessages.Add(userMsgEntity);

        var botMsgEntity = new ChatMessage
        {
            SessionId = sessionId,
            Role = ChatRole.Assistant,
            Content = botResponse,
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        };
        _context.ChatMessages.Add(botMsgEntity);

        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.UpdatedAt = DateTime.UtcNow;
            if (session.Title == "Cuộc trò chuyện mới")
            {
                session.Title = GenerateSessionTitle(userMessage);
            }
        }

        await _context.SaveChangesAsync();
    }

    private string GenerateSessionTitle(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return "Cuộc trò chuyện mới";
        var clean = userMessage.Trim();
        if (clean.Length <= 40) return clean;
        return clean.Substring(0, 37) + "...";
    }
}
