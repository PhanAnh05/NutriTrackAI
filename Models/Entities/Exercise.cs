using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NutriTrackAI.Models.Entities;

/// <summary>
/// Bảng 11: exercises - Danh mục bài tập vận động hệ thống
/// </summary>
[Table("exercises")]
public class Exercise
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MET tham khảo ở mức Vừa. Cường độ được quy đổi trước khi tính
    /// calo vận động thuần = max(MET - 1, 0) * 3.5 * kg / 200 * số phút.
    /// </summary>
    [Range(1.0, 25.0)]
    public decimal MetValue { get; set; } = 4.0m;

    public bool IsActive { get; set; } = true;

    public virtual ICollection<ExerciseLog> ExerciseLogs { get; set; } = new List<ExerciseLog>();
}
