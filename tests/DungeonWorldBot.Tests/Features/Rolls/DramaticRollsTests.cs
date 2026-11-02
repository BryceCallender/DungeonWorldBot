using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Tests.Features.Rolls;

public class DramaticRollsTests
{
    private static DramaticRoll? For(int first, int second, int modifier = 0, string? move = null, int missesBefore = 0, Health? health = null)
    {
        var dice = new MoveDice(first, second, first + second + modifier);
        return DramaticRolls.For(dice, move, dice.Outcome, missesBefore, health, pick: _ => 0);
    }

    [Fact]
    public void OrdinaryRolls_AreNotDramatic()
    {
        Assert.Null(For(3, 4));
        Assert.Null(For(5, 6, 1));
        Assert.Null(For(2, 3));
    }

    [Fact]
    public void DoubleSixes_AndSnakeEyes_AreDramatic()
    {
        Assert.Equal(DramaKind.DoubleSixes, For(6, 6)?.Kind);
        Assert.Equal(DramaKind.SnakeEyes, For(1, 1, 3)?.Kind);
    }

    [Theory]
    [InlineData(5, 6, DramaKind.LastBreathLived)]
    [InlineData(3, 4, DramaKind.LastBreathBargain)]
    [InlineData(1, 2, DramaKind.LastBreathDied)]
    [InlineData(6, 6, DramaKind.LastBreathLived)]
    public void LastBreath_IsAlwaysDramatic_ByHowItLands(int first, int second, DramaKind kind)
    {
        var drama = For(first, second, move: "Last Breath");

        Assert.Equal(kind, drama?.Kind);
        Assert.True(drama!.IsLastBreath);
    }

    [Fact]
    public void StrongHit_AfterARunOfMisses_IsAComeback()
    {
        Assert.Equal(DramaKind.Comeback, For(5, 5, missesBefore: 3)?.Kind);
        Assert.Null(For(5, 5, missesBefore: 2));
        Assert.Null(For(3, 4, missesBefore: 5));
    }

    [Fact]
    public void Miss_AtLowHP_IsDire()
    {
        Assert.Equal(DramaKind.DireMiss, For(2, 3, health: new Health { CurrentHP = 4, MaxHP = 20 })?.Kind);
        Assert.Null(For(2, 3, health: new Health { CurrentHP = 10, MaxHP = 20 }));
        // At 0 HP it's Last Breath's turn, not this
        Assert.Null(For(2, 3, health: new Health { CurrentHP = 0, MaxHP = 20 }));
    }

    [Fact]
    public void Reveal_PutsTheHeadlineFirst_AndKeepsTheTitleAndDice()
    {
        var roll = new Embed(
            Title: "🎲 Edena: Hack and Slash",
            Description: "```\nd6 + d6\n 6 +  6 = 12\n```",
            Fields: new List<Remora.Discord.API.Abstractions.Objects.IEmbedField> { new EmbedField(RollLabel.RolledWith, "STR +1") });

        var revealed = For(6, 6)!.Reveal(roll);
        var fields = revealed.Fields.Value;

        Assert.Equal("⚡ DOUBLE SIXES ⚡", fields[0].Name);
        Assert.Equal(RollLabel.RolledWith, fields[1].Name);
        Assert.Equal(roll.Title, revealed.Title);
        Assert.Equal(roll.Description, revealed.Description);
        Assert.Equal("Hack and Slash", RollLabel.Of(revealed).Move);
    }

    [Fact]
    public void Teaser_IsNeverReadAsARoll()
    {
        var teaser = For(1, 1)!.Teaser("Edena", "Hack and Slash");

        Assert.False(teaser.Title.HasValue);
        Assert.Null(MoveDice.FromDescription(teaser.Description.Value));
    }

    [Fact]
    public void OnlyOutcomeHeadlines_GoWhenTheOutcomeChanges()
    {
        Assert.True(DramaticRolls.DependsOnOutcome(For(1, 2, move: "Last Breath")!.Headline));
        Assert.True(DramaticRolls.DependsOnOutcome(For(5, 5, missesBefore: 3)!.Headline));
        Assert.True(DramaticRolls.DependsOnOutcome(For(2, 3, health: new Health { CurrentHP = 2, MaxHP = 20 })!.Headline));
        Assert.False(DramaticRolls.DependsOnOutcome(For(6, 6)!.Headline));
        Assert.False(DramaticRolls.DependsOnOutcome(For(1, 1)!.Headline));
    }
}
