using SkillForge.Core.Skills;

namespace SkillForge.Games;

public interface IGamePlugin
{
    string Id { get; }
    string Name { get; }
    string Category { get; } // "memory", "speed", "precision", "mixed"
    
    // Game data generation
    object GenerateData(int difficulty, int round);
    
    // Validation
    ValidationResult ValidateAnswer(object answer, object expected);
    
    // Scoring
    int CalculateScore(int timeMs, int accuracy, bool isPerfect);
    
    // Skill impact
    SkillImpact GetSkillImpact();
}

public record ValidationResult(
    int CorrectCount,
    int TotalCount,
    bool IsPerfect
);

public record SkillImpact(
    double MemoryWeight,
    double SpeedWeight,
    double PrecisionWeight
);
