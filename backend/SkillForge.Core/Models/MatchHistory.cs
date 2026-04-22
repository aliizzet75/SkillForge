using System.ComponentModel.DataAnnotations;

namespace SkillForge.Core.Models;

public class MatchHistory
{
    [Key]
    public Guid MatchId { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public int GameType { get; set; }
    
    [Required]
    public int Score { get; set; }
    
    [Required]
    public bool Won { get; set; }
    
    [Required]
    public int XPEarned { get; set; }
    
    [Required]
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public User? User { get; set; }
}