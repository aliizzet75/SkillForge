using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillForge.Core.Models;

public class SkillSnapshot
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [Required]
    [MaxLength(20)]
    public string SkillType { get; set; } = string.Empty;

    public int XP { get; set; }
    public int Level { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Percentile { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
