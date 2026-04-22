namespace SkillForge.Core.Models;

public class RoundPlayerResult
{
    public int Score { get; set; }
    public int TotalScore { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
    public bool IsPerfect { get; set; }
    public int TimeMs { get; set; }
}
