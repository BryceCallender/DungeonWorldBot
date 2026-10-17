using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Combat;

public sealed class ArmorTests : IDisposable
{
    private static readonly Snowflake UserId = new(77);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static async Task<Character> AddCharacterAsync(DungeonWorldContext context, int physical = 2, int magical = 1, int divine = 0)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Arthur",
            Class = new Class { Type = ClassType.Paladin },
            Health = new Health { CurrentHP = 20, MaxHP = 20 },
            Inventory = new Inventory(),
            Stats = [],
            ArmorRating = physical,
            MagicArmor = magical,
            DivineArmor = divine,
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    [Theory]
    [InlineData(9, 2, 0, 7)]
    [InlineData(9, 2, 1, 8)]
    [InlineData(9, 2, 5, 9)]
    [InlineData(1, 3, 0, 0)]
    [InlineData(0, 0, 0, 0)]
    public void Taken_IsDamageMinusArmorLessPiercing(int damage, int armor, int piercing, int expected) =>
        Assert.Equal(expected, DamageRules.Taken(damage, armor, piercing));

    [Theory]
    [InlineData(DamageType.Physical, 13)]
    [InlineData(DamageType.Magical, 14)]
    [InlineData(DamageType.Divine, 15)]
    [InlineData(DamageType.True, 10)]
    public async Task Damage_IsOnlyReducedByMatchingArmor(DamageType type, int expectedHp)
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context, physical: 3, magical: 4, divine: 5);

        var hit = await new ArmorService(context).TakeDamageAsync(character, 10, type);

        Assert.Equal(20 - expectedHp, hit.Taken);
        Assert.Equal(expectedHp, character.Health.CurrentHP);
    }

    [Fact]
    public async Task TemporaryArmor_StacksWithItsTypeOnly()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context, physical: 2, magical: 0);
        var armor = new ArmorService(context);

        await armor.AddBonusAsync(character, "Guardian's Stand", ArmorType.Magical, 1, ArmorDuration.Fight);
        await armor.AddBonusAsync(character, "Guardian's Stand", ArmorType.Magical, 1, ArmorDuration.Fight);
        await armor.AddBonusAsync(character, "Cover", ArmorType.Physical, 2, ArmorDuration.Fight);

        Assert.Equal(4, DamageRules.Armor(character, ArmorType.Physical));
        Assert.Equal(2, DamageRules.Armor(character, ArmorType.Magical));
        Assert.Equal("Physical 4 · Magical 2", DamageRules.Totals(character));
    }

    [Fact]
    public async Task NegativeArmor_NeverDropsBelowZero()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context, physical: 1);
        await new ArmorService(context).AddBonusAsync(character, "Sundered", ArmorType.Physical, -3, ArmorDuration.UntilRemoved);

        Assert.Equal(0, DamageRules.Armor(character, ArmorType.Physical));
    }

    [Fact]
    public async Task UntilHitArmor_CountsForTheHitThenBreaks()
    {
        await using (var context = CreateContext())
        {
            var character = await AddCharacterAsync(context, physical: 0, magical: 0);
            var armor = new ArmorService(context);
            await armor.AddBonusAsync(character, "Lightning barrier", ArmorType.Magical, 1, ArmorDuration.UntilHit);
            await armor.AddBonusAsync(character, "Shield wall", ArmorType.Physical, 2, ArmorDuration.Fight);

            var hit = await armor.TakeDamageAsync(character, 5, DamageType.Magical);

            Assert.Equal(4, hit.Taken);
            Assert.Equal("Lightning barrier", Assert.Single(hit.Broken).Source);
        }

        await using (var context = CreateContext())
        {
            var character = (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!;
            Assert.Equal(16, character.Health.CurrentHP);
            Assert.Equal("Shield wall", Assert.Single(character.ArmorBonuses!).Source);
        }
    }

    [Fact]
    public async Task EndFight_KeepsArmorUntilRemoved()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context);
        var armor = new ArmorService(context);
        await armor.AddBonusAsync(character, "Shield wall", ArmorType.Physical, 2, ArmorDuration.Fight);
        await armor.AddBonusAsync(character, "Lightning barrier", ArmorType.Magical, 1, ArmorDuration.UntilHit);
        await armor.AddBonusAsync(character, "Ring of Warding", ArmorType.Magical, 1, ArmorDuration.UntilRemoved);

        var removed = await armor.EndFightAsync(character);

        Assert.Equal(2, removed.Count);
        Assert.Equal("Ring of Warding", Assert.Single(character.ArmorBonuses!).Source);

        Assert.Single(await armor.RemoveBonusesAsync(character, "ring of warding"));
        Assert.Empty(character.ArmorBonuses!);
    }

    [Fact]
    public void Describe_ShowsTheWorking()
    {
        Assert.Equal("9 magical − 2 magical armor = **7**",
            DamageRules.Describe(new DamageTaken(9, DamageType.Magical, 2, 0, 7, [])));
        Assert.Equal("9 physical − (3 physical armor − 1 piercing) = **7**",
            DamageRules.Describe(new DamageTaken(9, DamageType.Physical, 3, 1, 7, [])));
        Assert.Equal("4 true damage, ignoring armor = **4**",
            DamageRules.Describe(new DamageTaken(4, DamageType.True, 0, 0, 4, [])));
        Assert.Equal("6 divine − 2 divine armor = **4**",
            DamageRules.Describe(new DamageTaken(6, DamageType.Divine, 2, 0, 4, [])));
    }

    [Fact]
    public async Task SetArmor_SetsDivineArmor()
    {
        await using var context = CreateContext();
        var character = await AddCharacterAsync(context);

        await new ArmorService(context).SetArmorAsync(character, ArmorType.Divine, 3);

        Assert.Equal(3, character.DivineArmor);
        Assert.Equal(1, character.MagicArmor);
        Assert.Equal("Physical 2 · Magical 1 · Divine 3", DamageRules.Totals(character));
    }
}
