using System.Reflection;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes.DungeonWorld;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using d20Tek.DiceNotation.DieRoller;

namespace DungeonWorldBot.Tests.Features.Moves;

public class MoveTests
{
    public static IEnumerable<object[]> AllMoves() =>
        typeof(BasicMoves)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            // Each move, not the All list of them
            .Where(f => f.FieldType == typeof(MoveDefinition))
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
        var parser = new DiceRoller();
        for (var i = 0; i < 200; i++)
        {
            var roll = MoveRoller.Roll(parser, modifier);
            Assert.InRange(roll.Dice.Value, min, max);
            Assert.Equal(MoveRoller.OutcomeFor(roll.Dice.Value), roll.Outcome);
        }
    }

    [Theory]
    [InlineData("2d6", 5, MoveOutcome.StrongHit)]      // 10
    [InlineData("2d6+1", 3, MoveOutcome.WeakHit)]      // 7
    [InlineData("2d6 - 1", 3, MoveOutcome.Miss)]       // 5
    [InlineData("2D6+2-1", 4, MoveOutcome.WeakHit)]    // 9
    public void OutcomeOf_TwoD6Rolls_HaveABand(string expression, int everyDie, MoveOutcome expected)
    {
        var roll = new DiceRoller(new ConstantDieRoller(everyDie)).Roll(expression);

        Assert.Equal(expected, MoveRoller.OutcomeOf(roll));
    }

    [Fact]
    public void AppetiteRolls_Use1d6Plus1d8_AndHaveABand()
    {
        var roll = MoveRoller.Roll(new DiceRoller(new ConstantDieRoller(4)), 1, appetite: true);

        Assert.StartsWith(MoveRoller.AppetiteDice, roll.Dice.DiceExpression);
        Assert.Equal(9, roll.Dice.Value);
        Assert.Equal(MoveOutcome.WeakHit, MoveRoller.OutcomeOf(roll.Dice));
    }

    [Theory]
    [InlineData(5, 3, true)]
    [InlineData(3, 5, false)]
    [InlineData(4, 4, false)]
    public void AppetiteBites_WhenTheD6IsHigher(int d6, int d8, bool bites)
    {
        var roll = MoveRoller.Roll(new DiceRoller(new SequenceDieRoller(d6, d8)), 0, appetite: true);

        Assert.Equal(bites, MoveRoller.AppetiteBites(roll.Dice));
    }

    [Fact]
    public void Appetites_AreTheBarbariansChosenOnes()
    {
        var barbarian = new Character
        {
            Name = "Grok",
            Abilities = [new Ability { Name = "Appetite: Conquest" }, new Ability { Name = "Musclebound" }, new Ability { Name = "Appetite: Fame and Glory" }],
        };

        Assert.Equal(["Conquest", "Fame and Glory"], Barbarian.Appetites(barbarian));
        Assert.Empty(Barbarian.Appetites(new Character { Name = "Fighter" }));
    }

    // Rolls the given values in order, one per die
    private sealed class SequenceDieRoller(params int[] values) : d20Tek.DiceNotation.IDieRoller
    {
        private int _next;

        public int Roll(int sides, int? factor = null) => values[_next++ % values.Length];
    }

    [Theory]
    [InlineData("d20")]
    [InlineData("1d6")]
    [InlineData("3d6")]
    [InlineData("4d6k3")]
    [InlineData("2d8")]
    [InlineData("2d6+1d4")]
    [InlineData("2d6x2")]
    public void OutcomeOf_OtherRolls_HaveNoBand(string expression)
    {
        var roll = new DiceRoller(new ConstantDieRoller(1)).Roll(expression);

        Assert.Null(MoveRoller.OutcomeOf(roll));
    }

    [Theory]
    [MemberData(nameof(AllMoves))]
    public void EveryMove_HasTextForEveryOutcome(MoveDefinition move)
    {
        Assert.All(Enum.GetValues<MoveOutcome>(), outcome => Assert.False(string.IsNullOrWhiteSpace(move.Describe(outcome)), $"{move.Name}: {outcome}"));
        Assert.False(string.IsNullOrWhiteSpace(move.Trigger));
    }

    [Theory]
    [InlineData(MoveOutcome.StrongHit, false)]
    [InlineData(MoveOutcome.WeakHit, true)]
    [InlineData(MoveOutcome.Miss, false)]
    public void Volley_OnlyShowsItsCostsOnAWeakHit(MoveOutcome outcome, bool shown)
    {
        Assert.Equal(shown, BasicMoves.Volley.ShowsOptions(outcome));
    }

    [Theory]
    [InlineData(MoveOutcome.StrongHit, true)]
    [InlineData(MoveOutcome.WeakHit, true)]
    [InlineData(MoveOutcome.Miss, false)]
    public void DiscernRealities_ShowsItsQuestionsOnAnyHit(MoveOutcome outcome, bool shown)
    {
        Assert.Equal(shown, BasicMoves.DiscernRealities.ShowsOptions(outcome));
    }
}
