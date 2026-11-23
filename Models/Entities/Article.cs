using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 17: articles - Bài viết dinh dưỡng, FAQ (phục vụ RAG) và Cảnh báo hệ thống
/// </summary>
[Table("articles")]
public class Article
{
    [Key]
    public int Id { get; set; }

    [Required]
    public ArticleType Type { get; set; } = ArticleType.Article;

    [Required]
    [StringLength(250)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Lưu vector embedding dạng JSON (float[]) để phục vụ truy xuất RAG không cần cài đặt vector DB rời
    /// </summary>
    public string? EmbeddingJson { get; set; }

    public bool IsPublished { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
