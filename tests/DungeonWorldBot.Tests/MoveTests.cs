using System.Reflection;
using DiceNotation;
using DungeonWorldBot.Moves;

namespace DungeonWorldBot.Tests;

public class MoveTests
{
    public static IEnumerable<object[]> AllMoves() =>
        typeof(BasicMoves)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => new object[] { (MoveDefinition)f.GetValue(null)! });

    [Theory]
    [InlineData(2, MoveOutcome.Miss)]
    [InlineData(6, MoveOutcome.Miss)]
    [InlineData(7, MoveOutcome.WeakHit)]
    [InlineData(9, MoveOutcome.WeakHit)]
    [InlineData(10, MoveOutcome.StrongHit)]
    [InlineData(15, MoveOutcome.StrongHit)]
    public void OutcomeFor_UsesDungeonWorldBands(int total, MoveOutcome expected)
    {
        Assert.Equal(expected, MoveRoller.OutcomeFor(total));
    }

    [Theory]
    [InlineData(0, 2, 12)]
    [InlineData(3, 5, 15)]
    [InlineData(-2, 0, 10)]
    public void Roll_Adds_Modifier(int modifier, int min, int max)
    {
        var parser = new DiceParser();
        for (var i = 0; i < 200; i++)
        {
            var roll = MoveRoller.Roll(parser, modifier);
            Assert.InRange(roll.Dice.Value, min, max);
            Assert.Equal(MoveRoller.OutcomeFor(roll.Dice.Value), roll.Outcome);
        }
    }

    [Theory]
    [MemberData(nameof(AllMoves))]
    public void EveryMove_HasTextForEveryOutcome(MoveDefinition move)
    {
        Assert.All(Enum.GetValues<MoveOutcome>(), outcome => Assert.False(string.IsNullOrWhiteSpace(move.Describe(outcome)), $"{move.Name}: {outcome}"));
        Assert.False(string.IsNullOrWhiteSpace(move.Trigger));
    }
}
