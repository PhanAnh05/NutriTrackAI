using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 15: chat_sessions - Phiên trò chuyện với trợ lý AI
/// </summary>
[Table("chat_sessions")]
public class ChatSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "Cuộc trò chuyện mới";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hỗ trợ xóa mềm lịch sử chat từ phía người dùng
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
