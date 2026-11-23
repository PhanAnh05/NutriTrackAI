using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NutriTrackAI.Data;
using NutriTrackAI.Models.Entities;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Services;

public class RagService : IRagService
{
    private readonly AppDbContext _context;
    private readonly ILogger<RagService> _logger;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "là", "gì", "của", "cho", "với", "thì", "có", "không", "nên", "tôi", "bạn",
        "mình", "thế", "nào", "được", "hay", "và", "như", "khi", "trong", "để", "ở"
    };

    public RagService(AppDbContext context, ILogger<RagService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ArticleSearchResultDto>> SearchRelevantArticlesAsync(string query, int topK = 3)
    {
        topK = Math.Max(1, Math.Min(10, topK));

        var articles = await _context.Articles
            .Where(a => a.IsPublished)
            .ToListAsync();

        if (!articles.Any())
        {
            return new List<ArticleSearchResultDto>();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            // Trả về Disclaimer và các FAQ cơ bản nhất
            return articles
                .OrderBy(a => a.Type == ArticleType.Disclaimer ? 0 : 1)
                .Take(topK)
                .Select(a => MapToDto(a, 1.0))
                .ToList();
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();
        var tokens = normalizedQuery
            .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .Distinct()
            .ToList();

        var scoredList = new List<ArticleSearchResultDto>();

        foreach (var art in articles)
        {
            var titleLower = art.Title.ToLowerInvariant();
            var bodyLower = art.Body.ToLowerInvariant();
            double score = 0;

            // 1. Khớp chính xác cụm từ tìm kiếm trong Tiêu đề hoặc Nội dung
            if (titleLower.Contains(normalizedQuery))
            {
                score += 15.0;
            }
            if (bodyLower.Contains(normalizedQuery))
            {
                score += 8.0;
            }

            // 2. Khớp từng từ khóa (Tokens)
            foreach (var token in tokens)
            {
                if (titleLower.Contains(token))
                {
                    score += 3.5;
                }
                if (bodyLower.Contains(token))
                {
                    score += 1.0;
                }
            }

            // 3. Ưu tiên nhẹ bài viết FAQ/Disclaimer quan trọng
            if (art.Type == ArticleType.Disclaimer)
            {
                score += 0.5;
            }

            // 4. Nếu có EmbeddingJson, có thể tính Cosine Similarity (khi triển khai đầy đủ vector)
            if (!string.IsNullOrEmpty(art.EmbeddingJson))
            {
                // Có thể mở rộng Cosine Similarity tại đây khi nạp query embedding
            }

            if (score > 0)
            {
                scoredList.Add(MapToDto(art, score));
            }
        }

        // Nếu không có bài viết nào khớp từ khóa, fallback lấy các bài FAQ phổ biến nhất
        if (!scoredList.Any())
        {
            return articles
                .OrderBy(a => a.Type == ArticleType.Disclaimer ? 0 : 1)
                .Take(topK)
                .Select(a => MapToDto(a, 0.5))
                .ToList();
        }

        return scoredList
            .OrderByDescending(x => x.RelevanceScore)
            .Take(topK)
            .ToList();
    }

    public async Task<string> GetRagContextAsync(string query, int topK = 3)
    {
        var relevantArticles = await SearchRelevantArticlesAsync(query, topK);
        if (!relevantArticles.Any())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== [TÀI LIỆU NỘI BỘ & KIẾN THỨC DINH DƯỠNG THAM KHẢO CHUẨN (RAG)] ===");
        sb.AppendLine("Dưới đây là các tài liệu và câu trả lời chính thống từ cơ sở dữ liệu NutriTrackAI. Hãy ưu tiên sử dụng thông tin từ các tài liệu này để trả lời người dùng:");
        sb.AppendLine();

        int index = 1;
        foreach (var art in relevantArticles)
        {
            sb.AppendLine($"--- TÀI LIỆU {index++}: {art.Title} (Phân loại: {art.Type}) ---");
            sb.AppendLine(art.Body);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static ArticleSearchResultDto MapToDto(Article a, double score) => new()
    {
        Id = a.Id,
        Title = a.Title,
        Body = a.Body,
        Type = a.Type.ToString(),
        RelevanceScore = score
    };
}
