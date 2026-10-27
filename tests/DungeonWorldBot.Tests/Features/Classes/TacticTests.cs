using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Classes;

public class TacticTests
{
    private static Character Battlemaster(string? defaultTactic, string? active = null, bool knowsTactics = true)
    {
        var abilities = new List<Ability>();
        if (knowsTactics)
            abilities.Add(new Ability { Name = TacticRules.Tactics });
        if (defaultTactic is not null)
            abilities.Add(new Ability { Name = TacticRules.DefaultTacticPrefix + defaultTactic });
        if (active is not null)
            abilities.Add(new Ability { Name = TacticRules.ActiveTactic, Description = active });

        return new Character
        {
            ID = new Snowflake(1),
            Name = "Vesk",
            Class = new Class { Type = ClassType.Battlemaster },
            Health = new Health { CurrentHP = 20, MaxHP = 20 },
            Inventory = new Inventory(),
            Stats = [],
            Abilities = abilities,
        };
    }

    [Fact]
    public void Current_IsTheDefault_UntilChanged() =>
        Assert.Equal(TacticRules.Cautious, TacticRules.Current(Battlemaster("Cautious")));

    [Fact]
    public void Current_IsTheActiveTactic_AfterAChange() =>
        Assert.Equal(TacticRules.Reckless, TacticRules.Current(Battlemaster("Cautious", "Reckless")));

    [Fact]
    public void WithoutTactics_NothingApplies()
    {
        var character = Battlemaster("Reactive", knowsTactics: false);

        Assert.Null(TacticRules.Current(character));
        Assert.Null(TacticRules.WhyCannotMake(character, BasicMoves.HackAndSlash.Name));
    }

    [Fact]
    public void Cautious_AddsOneToDefyDanger_Only()
    {
        var character = Battlemaster("Cautious");

        Assert.Equal(1, TacticRules.RollBonus(character, BasicMoves.DefyDanger.Name));
        Assert.Equal(0, TacticRules.RollBonus(character, BasicMoves.Defend.Name));
    }

    [Fact]
    public void Reactive_BlocksHackAndSlash_AndAllowsCounterattack()
    {
        var character = Battlemaster("Reactive");

        Assert.NotNull(TacticRules.WhyCannotMake(character, BasicMoves.HackAndSlash.Name));
        Assert.Null(TacticRules.WhyCannotMake(character, TacticRules.Counterattack));
    }

    [Fact]
    public void Counterattack_NeedsReactive() =>
        Assert.NotNull(TacticRules.WhyCannotMake(Battlemaster("Aggressive"), TacticRules.Counterattack));

    [Fact]
    public void Defensive_TurnsAMissedDefendIntoAWeakHit()
    {
        var character = Battlemaster("Defensive");

        Assert.Equal(MoveOutcome.WeakHit, TacticRules.Outcome(character, BasicMoves.Defend.Name, MoveOutcome.Miss));
        Assert.Equal(MoveOutcome.Miss, TacticRules.Outcome(character, BasicMoves.DefyDanger.Name, MoveOutcome.Miss));
    }

    [Fact]
    public void Aggressive_HackAndSlashDamagesOnAMiss() =>
        Assert.True(TacticRules.DamagesOnMiss(Battlemaster("Aggressive"), BasicMoves.HackAndSlash.Name));

    [Fact]
    public void Reckless_AddsAD4() =>
        Assert.Equal("1d4", TacticRules.ExtraDamage(Battlemaster("Cautious", "Reckless")));

    [Fact]
    public void RecklessAggression_RollsDamageTwice_OnlyInAggressive()
    {
        var aggressive = Battlemaster("Aggressive");
        aggressive.Abilities!.Add(new Ability { Name = TacticRules.RecklessAggression });
        var reckless = Battlemaster("Aggressive", "Reckless");
        reckless.Abilities!.Add(new Ability { Name = TacticRules.RecklessAggression });

        Assert.True(TacticRules.RollsDamageTwice(aggressive));
        Assert.False(TacticRules.RollsDamageTwice(reckless));
        Assert.False(TacticRules.RollsDamageTwice(Battlemaster("Aggressive")));
    }
}
