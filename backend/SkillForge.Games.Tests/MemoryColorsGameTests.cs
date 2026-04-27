using SkillForge.Games;
using Xunit;

namespace SkillForge.Games.Tests;

public class MemoryColorsGameTests
{
    private readonly MemoryColorsGame _game = new();

    [Fact]
    public void GenerateData_ReturnsNonEmptyArray()
    {
        var data = _game.GenerateData(difficulty: 1, round: 1);
        
        var sequence = Assert.IsType<string[]>(data);
        Assert.NotEmpty(sequence);
    }

    [Fact]
    public void GenerateData_ReturnsValidColors()
    {
        var validColors = new[] { "🔴", "🟢", "🔵", "🟡", "🟣", "🟠", "⚫", "⚪" };
        
        var data = _game.GenerateData(difficulty: 1, round: 1);
        var sequence = Assert.IsType<string[]>(data);
        
        Assert.All(sequence, color => Assert.Contains(color, validColors));
    }

    [Fact]
    public void GenerateData_DifficultyIncreasesLength()
    {
        var easy = _game.GenerateData(difficulty: 1, round: 1);
        var hard = _game.GenerateData(difficulty: 3, round: 1);
        
        var easySequence = Assert.IsType<string[]>(easy);
        var hardSequence = Assert.IsType<string[]>(hard);
        
        Assert.True(hardSequence.Length > easySequence.Length);
    }

    [Fact]
    public void ValidateAnswer_PerfectMatch_ReturnsCorrectResult()
    {
        var expected = new[] { "🔴", "🟢", "🔵" };
        var answer = new[] { "🔴", "🟢", "🔵" };
        
        var result = _game.ValidateAnswer(answer, expected);
        
        Assert.Equal(3, result.CorrectCount);
        Assert.Equal(3, result.TotalCount);
        Assert.True(result.IsPerfect);
    }

    [Fact]
    public void ValidateAnswer_PartialMatch_ReturnsCorrectResult()
    {
        var expected = new[] { "🔴", "🟢", "🔵" };
        var answer = new[] { "🔴", "🟡", "🔵" };
        
        var result = _game.ValidateAnswer(answer, expected);
        
        Assert.Equal(2, result.CorrectCount);
        Assert.Equal(3, result.TotalCount);
        Assert.False(result.IsPerfect);
    }

    [Fact]
    public void ValidateAnswer_WrongAnswer_ReturnsZeroCorrect()
    {
        var expected = new[] { "🔴", "🟢", "🔵" };
        var answer = new[] { "🟡", "🟣", "🟠" };
        
        var result = _game.ValidateAnswer(answer, expected);
        
        Assert.Equal(0, result.CorrectCount);
        Assert.False(result.IsPerfect);
    }

    [Fact]
    public void ValidateAnswer_ShorterAnswer_HandlesGracefully()
    {
        var expected = new[] { "🔴", "🟢", "🔵" };
        var answer = new[] { "🔴", "🟢" };
        
        var result = _game.ValidateAnswer(answer, expected);
        
        Assert.Equal(2, result.CorrectCount);
        Assert.Equal(3, result.TotalCount);
        Assert.False(result.IsPerfect);
    }

    [Theory]
    [InlineData(1000, 100, true, 4400)]  // Perfect, fast
    [InlineData(5000, 100, true, 3900)]  // Perfect, slow
    [InlineData(1000, 80, false, 4200)]  // 80% accuracy
    public void CalculateScore_ReturnsExpectedValues(int timeMs, int accuracy, bool isPerfect, int expectedMin)
    {
        var score = _game.CalculateScore(timeMs, accuracy, isPerfect);
        
        Assert.True(score >= expectedMin, $"Expected score >= {expectedMin}, got {score}");
    }

    [Fact]
    public void CalculateScore_PerfectBonus_Adds500()
    {
        var scoreWithoutPerfect = _game.CalculateScore(5000, 100, false);
        var scoreWithPerfect = _game.CalculateScore(5000, 100, true);
        
        Assert.Equal(500, scoreWithPerfect - scoreWithoutPerfect);
    }

    [Fact]
    public void GetSkillImpact_ReturnsCorrectWeights()
    {
        var impact = _game.GetSkillImpact();
        
        Assert.Equal(0.8, impact.MemoryWeight);
        Assert.Equal(0.2, impact.SpeedWeight);
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
        Assert.Equal("memory_colors", _game.Id);
        Assert.Equal("Farben-Memory", _game.Name);
        Assert.Equal("memory", _game.Category);
    }
}
