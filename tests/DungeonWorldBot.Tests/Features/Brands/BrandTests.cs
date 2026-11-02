using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Brands;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Brands;

public sealed class BrandTests : IDisposable
{
    private static readonly Snowflake UserId = new(33);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static async Task<Character> AddImmolatorAsync(DungeonWorldContext context)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Ember",
            Class = new Class { Type = ClassType.Immolator },
            Health = new Health { CurrentHP = 18, MaxHP = 18 },
            Inventory = new Inventory(),
            Stats = [],
            Abilities = [new Ability { Name = BrandRules.BurningBrand }],
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    [Fact]
    public void Forge_KeepsTheAlwaysTags_AndTheChosenOnes()
    {
        var brand = BrandRules.Forge(["thrown, near", BrandRules.PlusOneDamage]);

        Assert.Equal("Burning Brand (fiery, touch, dangerous, thrown, near, +1 damage)", brand.Name);
        Assert.Equal(1, brand.Damage);
        Assert.DoesNotContain("dangerous", BrandRules.Forge([BrandRules.RemoveDangerous]).Name);
    }

    [Theory]
    [InlineData(MoveOutcome.StrongHit, 2)]
    [InlineData(MoveOutcome.WeakHit, 1)]
    [InlineData(MoveOutcome.Miss, 0)]
    public void Tags_FollowTheRoll(MoveOutcome outcome, int tags) =>
        Assert.Equal(tags, BrandRules.TagsFor(outcome));

    [Fact]
    public async Task ConjuredBrand_IsSaved_AndGuttersOutAfterThreeAttacks()
    {
        await using var context = _db.CreateContext();
        var character = await AddImmolatorAsync(context);
        var resources = new ResourceService(context);
        var service = new BrandService(context, resources);

        await service.ConjureAsync(character, ["hand", BrandRules.PlusOneDamage]);

        await using (var reloaded = _db.CreateContext())
        {
            var saved = (await new CharacterService(reloaded).GetCharacterFromUserAsync(UserId))!;
            Assert.Equal(1, BrandRules.Brand(saved)!.Damage);
            Assert.Equal(BrandRules.Uses, new ResourceService(reloaded).GetValue(saved, BrandRules.UsesResource));
        }

        Assert.Equal(2, await service.SpendAsync(character));
        Assert.Equal(1, await service.SpendAsync(character));
        Assert.Equal(0, await service.SpendAsync(character));
        Assert.Null(BrandRules.Brand(character));
    }

    [Fact]
    public async Task ConjuringAgain_ReplacesTheOldBrand()
    {
        await using var context = _db.CreateContext();
        var character = await AddImmolatorAsync(context);
        var service = new BrandService(context, new ResourceService(context));

        await service.ConjureAsync(character, ["hand"]);
        await service.ConjureAsync(character, ["thrown, near"]);

        Assert.Single(character.Inventory.Items!.OfType<Weapon>());
        Assert.Contains("thrown", BrandRules.Brand(character)!.Name);
    }
}
