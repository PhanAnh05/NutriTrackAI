namespace NutriTrackAI.Services;

public interface IRagService
{
    /// <summary>
    /// Truy xuất top K bài viết / FAQ phù hợp nhất từ bảng articles
    /// </summary>
    Task<List<ArticleSearchResultDto>> SearchRelevantArticlesAsync(string query, int topK = 3);

    /// <summary>
    /// Xuất chuỗi định dạng khối [TÀI LIỆU NỘI BỘ & FAQ] để nạp vào Gemini System Prompt
    /// </summary>
    Task<string> GetRagContextAsync(string query, int topK = 3);
}

public class ArticleSearchResultDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
}
