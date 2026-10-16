using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Debilities;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Debilities;

public sealed class DebilityTests : IDisposable
{
    private static readonly Snowflake UserId = new(99);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static Character NewCharacter(ClassType type, params string[] moves) => new()
    {
        ID = UserId,
        Name = "Ren Sow",
        Class = new Class { Type = type },
        Health = new Health { CurrentHP = 20, MaxHP = 20 },
        Inventory = new Inventory(),
        Stats = [],
        Abilities = moves.Select(m => new Ability { Name = m }).ToList(),
    };

    [Theory]
    [InlineData(DebilityType.Weak, StatType.Str)]
    [InlineData(DebilityType.Shaky, StatType.Dex)]
    [InlineData(DebilityType.Sick, StatType.Con)]
    [InlineData(DebilityType.Stunned, StatType.Int)]
    [InlineData(DebilityType.Confused, StatType.Wis)]
    [InlineData(DebilityType.Scarred, StatType.Cha)]
    public void EachDebility_MatchesItsStat(DebilityType debility, StatType stat)
    {
        Assert.Equal(stat, DebilityRules.StatFor(debility));
        Assert.Equal(debility, DebilityRules.For(stat));
    }

    [Fact]
    public async Task Debilities_SaveAndApplyToRolls()
    {
        await using (var context = CreateContext())
        {
            var character = NewCharacter(ClassType.Slayer);
            await new CharacterService(context).AddCharacterAsync(character);
            var debilities = new DebilityService(context);

            Assert.True(await debilities.AddAsync(character, DebilityType.Weak));
            Assert.False(await debilities.AddAsync(character, DebilityType.Weak));
            Assert.True(await debilities.AddAsync(character, DebilityType.Scarred));
        }

        await using (var context = CreateContext())
        {
            var character = (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!;

            Assert.Equal([DebilityType.Weak, DebilityType.Scarred], DebilityRules.Current(character));
            Assert.Equal((DebilityType.Weak, -1), DebilityRules.RollPenalty(character, StatType.Str));
            Assert.Null(DebilityRules.RollPenalty(character, StatType.Dex));

            var debilities = new DebilityService(context);
            Assert.True(await debilities.RemoveAsync(character, DebilityType.Weak));
            Assert.False(await debilities.RemoveAsync(character, DebilityType.Weak));
            Assert.Equal([DebilityType.Scarred], await debilities.ClearAsync(character));
            Assert.Empty(DebilityRules.Current(character));
        }
    }

    [Fact]
    public async Task Tenacity_IgnoresConDebilities()
    {
        await using var context = CreateContext();
        var character = NewCharacter(ClassType.Slayer, "Tenacity");
        await new CharacterService(context).AddCharacterAsync(character);
        await new DebilityService(context).AddAsync(character, DebilityType.Sick);

        Assert.Null(DebilityRules.RollPenalty(character, StatType.Con));
        Assert.Equal("Tenacity", DebilityRules.IgnoringMove(character, StatType.Con)?.Name);
    }
}
