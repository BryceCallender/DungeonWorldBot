using DiceNotation;
using DungeonWorldBot.Helpers;

namespace DungeonWorldBot.Tests;

public class DiceHelperTests
{
    private readonly IDiceParser _parser = new DiceParser();

    [Theory]
    [InlineData("2d6", 2, "2d6+2")]
    [InlineData("2d6", -1, "2d6-1")]
    [InlineData("2d6", 0, "2d6")]
    public void AddModifier_FormatsSign(string expression, int modifier, string expected)
    {
        Assert.Equal(expected, DiceHelper.AddModifier(expression, modifier));
    }

    [Theory]
    [InlineData("2d6+1", 3, 13)]
    [InlineData("2d6-1", 1, 11)]
    [InlineData("1d10", 1, 10)]
    [InlineData("4", 4, 4)]
    public void TryRoll_ValidExpression_RollsInRange(string expression, int min, int max)
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.True(DiceHelper.TryRoll(_parser, expression, out var result));
            Assert.InRange(result.Value, min, max);
        }
    }

    [Fact]
    public void TryRoll_D6_CanRollASix()
    {
        var sawSix = Enumerable.Range(0, 500)
            .Any(_ => DiceHelper.TryRoll(_parser, "1d6", out var result) && result.Value == 6);

        Assert.True(sawSix);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hi")]
    [InlineData("..")]
    public void TryRoll_InvalidExpression_ReturnsFalse(string expression)
    {
        Assert.False(DiceHelper.TryRoll(_parser, expression, out _));
    }
}
