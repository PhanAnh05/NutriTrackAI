using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NutriTrackAI.Models.Enums;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 16: chat_messages - Tin nhắn chi tiết trong từng phiên chat
/// </summary>
[Table("chat_messages")]
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int SessionId { get; set; }

    [Required]
    public ChatRole Role { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public int? TokensOptional { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ChatSession Session { get; set; } = null!;
}
