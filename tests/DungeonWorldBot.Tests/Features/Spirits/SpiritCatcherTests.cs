using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Spirits;

public sealed class SpiritCatcherTests : IDisposable
{
    private static readonly Snowflake UserId = new(22);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static Character Shaman(string? description) => new()
    {
        Name = "Willow",
        Abilities = [new Ability { Name = SpiritCatcherRules.SpiritCatcher, Description = description }],
    };

    [Fact]
    public void EmptyCatcher_HoldsNoSpirit()
    {
        var shaman = Shaman(SpiritCatcherRules.Move.Summary);

        Assert.Null(SpiritCatcherRules.Spirit(shaman));
        Assert.Null(SpiritCatcherRules.Effect(shaman));
    }

    [Fact]
    public void CaughtSpirit_IsReadBackWithItsEffect()
    {
        var caught = Shaman(SpiritCatcherRules.Describe("a dire wolf", null));
        Assert.Equal("a dire wolf", SpiritCatcherRules.Spirit(caught));
        Assert.Null(SpiritCatcherRules.Effect(caught));

        var told = Shaman(SpiritCatcherRules.Describe("a dire wolf", " A spectral wolf hunts one target for a scene. "));
        Assert.Equal("a dire wolf", SpiritCatcherRules.Spirit(told));
        Assert.Equal("A spectral wolf hunts one target for a scene.", SpiritCatcherRules.Effect(told));
    }

    [Fact]
    public async Task CaughtSpirit_IsSavedOnTheAbility()
    {
        await using (var context = _db.CreateContext())
        {
            var character = new Character
            {
                ID = UserId,
                Name = "Willow",
                Class = new Class { Type = ClassType.Shaman },
                Health = new Health { CurrentHP = 16, MaxHP = 16 },
                Inventory = new Inventory(),
                Stats = [],
                Abilities = [new Ability { Name = SpiritCatcherRules.SpiritCatcher, Description = SpiritCatcherRules.Move.Summary }],
            };
            await new CharacterService(context).AddCharacterAsync(character);
            await new AbilityService(context).SetDescriptionAsync(SpiritCatcherRules.Catcher(character)!, SpiritCatcherRules.Describe("a dire wolf", "It howls."));
        }

        await using var reloaded = _db.CreateContext();
        var saved = (await new CharacterService(reloaded).GetCharacterFromUserAsync(UserId))!;

        Assert.Equal("a dire wolf", SpiritCatcherRules.Spirit(saved));
        Assert.Equal("It howls.", SpiritCatcherRules.Effect(saved));
    }
}
