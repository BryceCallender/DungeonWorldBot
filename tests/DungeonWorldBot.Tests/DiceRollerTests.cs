using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Rolling;

namespace DungeonWorldBot.Tests;

public class DiceRollerTests
{
    private readonly IDiceRoller _roller = new DiceRoller();

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
    [InlineData("2d6-5", -3, 7)]   // not clamped to 1
    [InlineData("1d10", 1, 10)]
    [InlineData("4", 4, 4)]        // flat damage
    [InlineData("4d6k3", 3, 18)]
    [InlineData("4d6p1", 3, 18)]
    [InlineData("4d6l1", 1, 6)]
    [InlineData("2d6x2", 4, 24)]
    [InlineData("(2d6+1)/2", 1, 6)]
    [InlineData("4f", -4, 4)]
    [InlineData("d%", 1, 100)]
    [InlineData(" 2d6 + 1 ", 3, 13)]
    public void TryRoll_ValidExpression_RollsInRange(string expression, int min, int max)
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.True(_roller.TryRoll(expression, out var result), expression);
            Assert.InRange(result.Value, min, max);
        }
    }

    [Fact]
    public void TryRoll_ExplodingDice_CanExceedNormalMaximum()
    {
        var exceeded = Enumerable.Range(0, 2000)
            .Any(_ => _roller.TryRoll("1d6!", out var result) && result.Value > 6);

        Assert.True(exceeded);
    }

    [Theory]
    [InlineData("2d6!", "2d6!6")]
    [InlineData("1d6!", "1d6!6")]
    [InlineData("d20!+2", "d20!20+2")]
    [InlineData("3d6!5", "3d6!5")]   // explicit threshold left alone
    [InlineData("2d6+1", "2d6+1")]
    public void NormalizeExploding_SpellsOutDieMaximum(string expression, string expected)
    {
        Assert.Equal(expected, DiceRoller.NormalizeExploding(expression));
    }

    [Fact]
    public void ExplodingDice_OnlyExplodeOnTheTopFace()
    {
        // Every die shows 5, which must not explode on a d6 (the library's default would explode on 2+)
        var result = new DiceRoller(new ConstantDieRoller(5)).Roll("2d6!");

        Assert.Equal(2, result.Results.Count);
        Assert.Equal(10, result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hi")]
    [InlineData("..")]
    [InlineData("2d6+DungeonWorldBot.Data.Entities.Stat")] // what the old /roll stat sent
    [InlineData("d0")]
    [InlineData("101d6")]
    [InlineData("60d6+60d6")]
    [InlineData("d1001")]
    [InlineData("99999999999d6")]
    [InlineData("d1!")]                                    // would explode forever; library stops it
    [InlineData("1d6!1")]
    public void TryRoll_InvalidOrUnreasonable_ReturnsFalse(string expression)
    {
        Assert.False(_roller.TryRoll(expression, out _));
    }

    [Fact]
    public void Roll_UsesSuppliedDieRoller()
    {
        var roller = new DiceRoller(new ConstantDieRoller(4));

        Assert.Equal(9, roller.Roll("2d6+1").Value);
    }

    [Fact]
    public void Roll_InvalidExpression_Throws()
    {
        Assert.Throws<ArgumentException>(() => _roller.Roll("nope"));
    }

    [Theory]
    [InlineData("2d6", 4, "d6 + d6\n\n4 + 4 = **8**")]
    [InlineData("2d6+1", 4, "d6 + d6 + 1\n\n4 + 4 + 1 = **9**")]
    [InlineData("2d6-2", 4, "d6 + d6 - 2\n\n4 + 4 - 2 = **6**")]
    [InlineData("d20", 17, "d20\n\n**17**")]
    [InlineData("d20+3", 17, "d20 + 3\n\n17 + 3 = **20**")]
    [InlineData("4d6k3", 3, "d6 + d6 + d6 + ~~d6~~\n\n3 + 3 + 3 + ~~3~~ = **9**")]
    [InlineData("2d6x2", 3, "2d6x2\n\n3, 3 → **12**")]
    [InlineData("5", 1, "**5**")]
    public void BuildDiceResult_ShowsTheMath(string expression, int everyDie, string expected)
    {
        var result = new DiceRoller(new ConstantDieRoller(everyDie)).Roll(expression);

        Assert.Equal(expected, DiceHelper.BuildDiceResult(result));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(1000)]
    public void SecureDieRoller_StaysInRangeAndReachesEveryFace(int sides)
    {
        var roller = new SecureDieRoller();
        var seen = new HashSet<int>();
        var rolls = Math.Max(sides * 40, 1000);

        for (var i = 0; i < rolls; i++)
        {
            var value = roller.Roll(sides);
            Assert.InRange(value, 1, sides);
            seen.Add(value);
        }

        Assert.Equal(sides, seen.Count);
    }

    [Fact]
    public void SecureDieRoller_IsEvenlyDistributed()
    {
        // Chi-squared goodness of fit for a d6 over 60,000 rolls. With 5 degrees of freedom the critical value
        // at p = 0.001 is 20.52, so a fair die fails this about once in a thousand runs.
        const int sides = 6;
        const int rolls = 60_000;
        var roller = new SecureDieRoller();
        var counts = new int[sides];

        for (var i = 0; i < rolls; i++)
        {
            counts[roller.Roll(sides) - 1]++;
        }

        const double expected = (double)rolls / sides;
        var chiSquared = counts.Sum(c => (c - expected) * (c - expected) / expected);

        Assert.True(chiSquared < 20.52, $"chi² = {chiSquared:F2}, counts = [{string.Join(", ", counts)}]");
    }

    [Fact]
    public void SecureDieRoller_IsSafeToShareAcrossThreads()
    {
        var roller = new SecureDieRoller();

        var values = Enumerable.Range(0, 20_000).AsParallel().Select(_ => roller.Roll(20)).ToList();

        Assert.All(values, v => Assert.InRange(v, 1, 20));
        Assert.Equal(20, values.Distinct().Count());
    }
}
