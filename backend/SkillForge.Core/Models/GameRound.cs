using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillForge.Core.Models;

public class GameRound
{
    [Key]
    public long Id { get; set; }
    
    [Required]
    public Guid SessionId { get; set; }
    
    [ForeignKey("SessionId")]
    public GameSession Session { get; set; } = null!;
    
    public int RoundNumber { get; set; }
    
    public int? GameSubtype { get; set; } // 1=ingredients, 2=names, 3=colors
    
    public int? Player1TimeMs { get; set; }
    
    public int? Player2TimeMs { get; set; }
    
    public int? Player1Correct { get; set; }
    
    public int? Player2Correct { get; set; }
    
    [Column(TypeName = "jsonb")]
    public string? Player1Data { get; set; }
    
    [Column(TypeName = "jsonb")]
    public string? Player2Data { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime? EndedAt { get; set; }
}
