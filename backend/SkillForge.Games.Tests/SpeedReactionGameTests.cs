using SkillForge.Games;
using Xunit;

namespace SkillForge.Games.Tests;

public class SpeedReactionGameTests
{
    private readonly SpeedReactionGame _game = new();

    [Fact]
    public void GenerateData_ReturnsNonEmptyArray()
    {
        var data = _game.GenerateData(difficulty: 1, round: 1);
        
        var signal = Assert.IsType<string[]>(data);
        Assert.Single(signal);
    }

    [Fact]
    public void GenerateData_ReturnsValidSignal()
    {
        var validSignals = new[] { "⚡", "🔥", "💧", "🎯", "⭐", "🌟", "💥", "🎪" };
        
        var data = _game.GenerateData(difficulty: 1, round: 1);
        var signal = Assert.IsType<string[]>(data);
        
        Assert.Contains(signal[0], validSignals);
    }

    [Fact]
    public void GenerateData_Randomness_DifferentCallsMayReturnDifferentSignals()
    {
        // Run multiple times to verify randomness (though could theoretically be same)
        var signals = new HashSet<string>();
        for (int i = 0; i < 20; i++)
        {
            var data = _game.GenerateData(difficulty: 1, round: 1);
            var signal = Assert.IsType<string[]>(data);
            signals.Add(signal[0]);
        }
        
        Assert.True(signals.Count > 1, "Expected some variety in randomly generated signals");
    }

    [Fact]
    public void ValidateAnswer_AnyTap_IsCorrect()
    {
        var expected = new[] { "⚡" };
        var answer = new[] { "anything" };
        
        var result = _game.ValidateAnswer(answer, expected);
        
        Assert.Equal(1, result.CorrectCount);
        Assert.Equal(1, result.TotalCount);
        Assert.True(result.IsPerfect);
    }

    [Theory]
    [InlineData(0, 30000)]    // Immediate tap = max score
    [InlineData(100, 29000)]  // 100ms
    [InlineData(500, 25000)]  // 500ms
    [InlineData(1000, 20000)] // 1 second
    [InlineData(1500, 15000)] // 1.5 seconds
    [InlineData(2000, 10000)] // 2 seconds
    [InlineData(2500, 5000)]  // 2.5 seconds
    [InlineData(2999, 10)]    // Just under limit
    [InlineData(3000, 0)]     // At limit = 0
    [InlineData(3500, 0)]     // Over limit = 0
    [InlineData(5000, 0)]     // Way over limit = 0
    public void CalculateScore_ReturnsExpectedScore(int timeMs, int expectedScore)
    {
        var score = _game.CalculateScore(timeMs, accuracy: 100, isPerfect: true);
        
        Assert.Equal(expectedScore, score);
    }

    [Fact]
    public void CalculateScore_FasterIsBetter()
    {
        var fastScore = _game.CalculateScore(500, 100, true);
        var slowScore = _game.CalculateScore(2000, 100, true);
        
        Assert.True(fastScore > slowScore);
    }

    [Fact]
    public void CalculateScore_NeverNegative()
    {
        var score = _game.CalculateScore(10000, 100, true);
        
        Assert.Equal(0, score);
    }

    [Fact]
    public void GetSkillImpact_ReturnsCorrectWeights()
    {
        var impact = _game.GetSkillImpact();
        
        Assert.Equal(0.1, impact.MemoryWeight);
        Assert.Equal(0.9, impact.SpeedWeight);
        Assert.Equal(0.0, impact.PrecisionWeight);
    }

    [Fact]
    public void ImplementsIGamePlugin()
    {
        Assert.IsAssignableFrom<IGamePlugin>(_game);
    }

    [Fact]
    public void Properties_ReturnExpectedValues()
    {
        Assert.Equal("speed_reaction", _game.Id);
        Assert.Equal("Reaktion", _game.Name);
        Assert.Equal("speed", _game.Category);
    }

    [Fact]
    public void Category_IsSpeed()
    {
        Assert.Equal("speed", _game.Category);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    public void GenerateData_DifficultyDoesNotChangeOutputSize(int difficulty, int round)
    {
        // Speed game always shows 1 signal regardless of difficulty
        var data = _game.GenerateData(difficulty, round);
        var signal = Assert.IsType<string[]>(data);
        
        Assert.Single(signal);
    }
}
