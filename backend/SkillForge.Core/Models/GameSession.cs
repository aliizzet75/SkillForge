using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SkillForge.Core.Models;

public class GameSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid? Player1Id { get; set; }
    
    [ForeignKey("Player1Id")]
    public User? Player1 { get; set; }
    
    public Guid? Player2Id { get; set; }
    
    [ForeignKey("Player2Id")]
    public User? Player2 { get; set; }
    
    public bool Player2IsAi { get; set; } = false;
    
    [Required]
    [MaxLength(50)]
    public string GameType { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(20)]
    public string Mode { get; set; } = string.Empty; // "pvp", "ai", "solo"
    
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty; // "active", "completed", "aborted", "converted_solo"
    
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? EndedAt { get; set; }
    
    public Guid? WinnerId { get; set; }
    
    public int Player1Score { get; set; } = 0;
    
    public int Player2Score { get; set; } = 0;
    
    [Column(TypeName = "jsonb")]
    public string? Player1SkillDelta { get; set; }
    
    [Column(TypeName = "jsonb")]
    public string? Player2SkillDelta { get; set; }
    
    [Column(TypeName = "jsonb")]
    public string? DisconnectionInfo { get; set; }
    
    // Navigation properties
    public ICollection<GameRound> Rounds { get; set; } = new List<GameRound>();
}
