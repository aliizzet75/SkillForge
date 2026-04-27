using System.ComponentModel.DataAnnotations;

namespace SkillForge.Core.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? DisplayName { get; set; }
    
    [MaxLength(255)]
    public string? Email { get; set; }
    
    [MaxLength(255)]
    public string? PasswordHash { get; set; }
    
    [MaxLength(255)]
    public string? Avatar { get; set; }
    
    [MaxLength(20)]
    public string? SocialProvider { get; set; } // "google", "facebook", "github", etc.
    
    [MaxLength(255)]
    public string? SocialProviderId { get; set; } // External provider user ID
    
    [MaxLength(2)]
    public string? CountryCode { get; set; }
    
    [MaxLength(50)]
    public string? Timezone { get; set; }
    
    public int TotalXp { get; set; } = 0;
    
    public int CurrentLevel { get; set; } = 1;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastSeenAt { get; set; }
    
    public bool IsActive { get; set; } = true;

    [MaxLength(255)]
    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }
    
    // Navigation properties
    public ICollection<UserSkill> Skills { get; set; } = new List<UserSkill>();
    public ICollection<GameSession> GameSessionsAsPlayer1 { get; set; } = new List<GameSession>();
    public ICollection<GameSession> GameSessionsAsPlayer2 { get; set; } = new List<GameSession>();
}
