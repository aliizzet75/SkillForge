using SkillForge.Core.Skills;

namespace SkillForge.Games;

public class SpeedReactionGame : IGamePlugin
{
    public string Id => "speed_reaction";
    public string Name => "Reaktion";
    public string Category => "speed";

    private static readonly string[] Signals = { "⚡", "🔥", "💧", "🎯", "⭐", "🌟", "💥", "🎪" };
    private static readonly Random _rng = new();

    public object GenerateData(int difficulty, int round)
    {
        // One random signal — player must tap as fast as possible after it appears
        return new[] { Signals[_rng.Next(Signals.Length)] };
    }

    public ValidationResult ValidateAnswer(object answer, object expected)
    {
        // Any tap is correct — score is purely time-based
        return new ValidationResult(1, 1, true);
    }

    public int CalculateScore(int timeMs, int accuracy, bool isPerfect)
    {
        // 3 000 ms window; max 30 000 points (faster → more)
        return Math.Max(0, 3000 - timeMs) * 10;
    }

    public SkillImpact GetSkillImpact()
    {
        return new SkillImpact(0.1, 0.9, 0.0);
    }
}
