using SkillForge.Core.Skills;

namespace SkillForge.Games;

public class MemoryColorsGame : IGamePlugin
{
    public string Id => "memory_colors";
    public string Name => "Farben-Memory";
    public string Category => "memory";
    
    private static readonly string[] Colors = { "🔴", "🟢", "🔵", "🟡", "🟣", "🟠", "⚫", "⚪" };
    
    public object GenerateData(int difficulty, int round)
    {
        var count = 3 + difficulty + (round - 1); // More colors = harder
        var random = new Random();
        var sequence = Colors.OrderBy(_ => random.Next()).Take(count).ToArray();
        return sequence;
    }
    
    public ValidationResult ValidateAnswer(object answer, object expected)
    {
        var answerArray = (string[])answer;
        var expectedArray = (string[])expected;
        
        var correct = 0;
        for (int i = 0; i < Math.Min(answerArray.Length, expectedArray.Length); i++)
        {
            if (answerArray[i] == expectedArray[i]) correct++;
        }
        
        var isPerfect = correct == expectedArray.Length && answerArray.Length == expectedArray.Length;
        
        return new ValidationResult(correct, expectedArray.Length, isPerfect);
    }
    
    public int CalculateScore(int timeMs, int accuracy, bool isPerfect)
    {
        var accuracyBonus = accuracy * 100;
        var timeBonus = Math.Max(0, 10000 - timeMs) / 100;
        var perfectBonus = isPerfect ? 500 : 0;
        
        return accuracyBonus + timeBonus + perfectBonus;
    }
    
    public SkillImpact GetSkillImpact()
    {
        return new SkillImpact(0.8, 0.2, 0.0);
    }
}
