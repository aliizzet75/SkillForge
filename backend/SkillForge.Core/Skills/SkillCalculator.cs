namespace SkillForge.Core.Skills;

public enum SkillType
{
    Memory,
    Speed,
    Precision,
    Overall
}

public class SkillLevel
{
    public SkillType Type { get; set; }
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    public double Percentile { get; set; } = 50.0;
    public int GamesPlayed { get; set; } = 0;
    public int GamesWon { get; set; } = 0;
}

public class SkillCalculator
{
    // ELO-like system for skill calculation
    public static int CalculateXPChange(
        int playerScore,
        int opponentScore,
        int playerSkill,
        int opponentSkill,
        bool won)
    {
        // K-factor determines how quickly skills change
        const int kFactor = 32;
        
        // Expected score based on skill difference
        var expectedScore = 1 / (1 + Math.Pow(10, (opponentSkill - playerSkill) / 400.0));
        
        // Actual score (1 for win, 0 for loss, 0.5 for draw)
        var actualScore = won ? 1.0 : playerScore > opponentScore ? 0.5 : 0.0;
        
        // XP change
        var xpChange = (int)(kFactor * (actualScore - expectedScore));
        
        // Minimum XP gain for playing (participation)
        if (xpChange < 5) xpChange = 5;
        
        return xpChange;
    }
    
    public static int LevelFromXP(int xp)
    {
        // Level 1: 0-999 XP
        // Level 2: 1000-2999 XP
        // Level 3: 3000-5999 XP
        // etc.
        if (xp < 1000) return 1;
        
        var level = 1;
        var requiredXP = 1000;
        var remainingXP = xp;
        
        while (remainingXP >= requiredXP)
        {
            remainingXP -= requiredXP;
            level++;
            requiredXP = (int)(requiredXP * 1.5); // Increasing XP requirement
        }
        
        return level;
    }
    
    public static int XPForNextLevel(int currentXP)
    {
        var currentLevel = LevelFromXP(currentXP);
        var xpNeeded = 1000;
        
        for (int i = 1; i < currentLevel; i++)
        {
            xpNeeded = (int)(xpNeeded * 1.5);
        }
        
        return xpNeeded;
    }
}
