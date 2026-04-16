using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillForge.Core.Models;

public class UserSkill
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
    
    [Required]
    [MaxLength(20)]
    public string SkillType { get; set; } = string.Empty; // "memory", "speed", "precision", "overall"
    
    public int Level { get; set; } = 1;
    
    public int XP { get; set; } = 0;
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal Percentile { get; set; } = 50.00m;
    
    public int? RankGlobal { get; set; }
    
    public int? RankCountry { get; set; }
    
    public int GamesPlayed { get; set; } = 0;
    
    public int GamesWon { get; set; } = 0;
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
