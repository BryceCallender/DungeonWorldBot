using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Tests.Features.Moves;

public class CharacterMovesTests
{
    private static Character Paladin(int hp) => new()
    {
        Name = "Arthur",
        Class = new Class { Type = ClassType.Paladin },
        Health = new Health { CurrentHP = hp, MaxHP = 20 },
        Stats = [new Stat { StatType = StatType.Str, Value = 16 }],
    };

    [Fact]
    public void For_OffersEveryBasicMove_AimedAtWhoTheyNeed()
    {
        var moves = CharacterMoves.For(Paladin(hp: 20));

        Assert.Equal(MoveAim.Enemy, moves.Single(m => m.Name == BasicMoves.HackAndSlash.Name).Aim);
        Assert.Equal(MoveAim.Ally, moves.Single(m => m.Name == BasicMoves.AidOrInterfere.Name).Aim);
        Assert.Equal(MoveAim.None, moves.Single(m => m.Name == BasicMoves.SpoutLore.Name).Aim);
        Assert.True(moves.Single(m => m.Name == BasicMoves.DefyDanger.Name).PicksStat);
        Assert.All(moves, m => Assert.False(m.IsClassMove));
    }

    [Fact]
    public void For_OffersLastBreath_OnlyAtZeroHP()
    {
        Assert.DoesNotContain(CharacterMoves.For(Paladin(hp: 20)), m => m.Name == BasicMoves.LastBreath.Name);
        Assert.Contains(CharacterMoves.For(Paladin(hp: 0)), m => m.Name == BasicMoves.LastBreath.Name);
    }
}
