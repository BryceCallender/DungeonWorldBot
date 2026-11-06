using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes.InverseWorld;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Light;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Light;

public sealed class LightTests : IDisposable
{
    private static readonly Snowflake LanternId = new(44);
    private static readonly Snowflake AllyId = new(45);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static async Task<Character> AddCharacterAsync(DungeonWorldContext context, Snowflake id, ClassType type, params string[] moves)
    {
        var character = new Character
        {
            ID = id,
            Name = type.ToString(),
            Class = new Class { Type = type },
            Health = new Health { CurrentHP = 18, MaxHP = 18 },
            Inventory = new Inventory(),
            Stats = [new Stat { StatType = StatType.Int, Value = 16 }],
            Abilities = moves.Select(m => new Ability { Name = m }).ToList(),
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    private static LightService ServiceFor(DungeonWorldContext context) =>
        new(context, new ArmorService(context));

    [Fact]
    public async Task LendingTheShield_MovesItsArmorToTheAlly()
    {
        await using var context = _db.CreateContext();
        var lantern = await AddCharacterAsync(context, LanternId, ClassType.Lantern, Lantern.LightWeaponry);
        var ally = await AddCharacterAsync(context, AllyId, ClassType.Fighter);
        var service = ServiceFor(context);

        await service.RaiseShieldAsync(lantern, lantern);
        Assert.Equal(1, DamageRules.Armor(lantern, ArmorType.Physical));

        var shielded = await service.RaiseShieldAsync(lantern, ally);

        Assert.Equal([ally], shielded);
        Assert.Equal(0, DamageRules.Armor(lantern, ArmorType.Physical));
        Assert.Equal(1, DamageRules.Armor(ally, ArmorType.Physical));

        Assert.True(await service.LowerShieldAsync(lantern));
        Assert.Equal(0, DamageRules.Armor(ally, ArmorType.Physical));
    }

    [Fact]
    public async Task GiftFromGod_KeepsProtectingTheLanternWhileLent()
    {
        await using var context = _db.CreateContext();
        var lantern = await AddCharacterAsync(context, LanternId, ClassType.Lantern, Lantern.LightWeaponry, Lantern.GiftFromGod);
        var ally = await AddCharacterAsync(context, AllyId, ClassType.Fighter);

        var shielded = await ServiceFor(context).RaiseShieldAsync(lantern, ally);

        Assert.Equal(2, shielded.Count);
        Assert.Equal(1, DamageRules.Armor(lantern, ArmorType.Physical));
    }

    [Fact]
    public async Task ArrowPiercing_StartsAtTwo_OrThreeWithGiftFromGod_AndResetsAtCamp()
    {
        await using var context = _db.CreateContext();
        var lantern = await AddCharacterAsync(context, LanternId, ClassType.Lantern, Lantern.LightWeaponry);
        var resources = new ResourceService(context);
        var piercing = Lantern.Sheet.FindResource(Lantern.ArrowPiercing)!;

        Assert.True(piercing.ResetsOnCamp);
        Assert.Equal(2, resources.GetValue(lantern, piercing));

        lantern.Abilities.Add(new Ability { Name = Lantern.GiftFromGod });
        Assert.Equal(3, resources.GetResetValue(lantern, piercing));
    }

    [Fact]
    public async Task PiercingDamage_GetsThroughTheTargetsArmor()
    {
        await using var context = _db.CreateContext();
        var lantern = await AddCharacterAsync(context, LanternId, ClassType.Lantern, Lantern.LightWeaponry);
        var encounters = new EncounterService(context, TimeProvider.System);
        var fight = (await encounters.StartAsync("Ambush"))!;
        var goblin = new Enemy { Name = "Goblin", CurrentHP = 10, MaxHP = 10, Armor = 2, Damage = "d6" };
        await encounters.AddEnemyAsync(fight, goblin);

        // Every die lands on 5: 2d6+2 is a 12, and the Lantern's d6 damage is 5
        var dice = new DiceRoller(new ConstantDieRoller(5));
        var characters = new CharacterService(context);
        var damage = new DamageService(characters, dice, new GearService(new AbilityService(context), new ResourceService(context), dice), new ArmorService(context));
        var input = MoveInput.ForStat(lantern, StatType.Int, 0, target: "Goblin")! with { Piercing = 2 };

        var resolution = await new MoveResolver(dice, damage, encounters).ResolveAsync(lantern, BasicMoves.Volley, input);

        Assert.Equal(5, resolution.Dealt!.Taken);
        Assert.Equal(5, goblin.CurrentHP);
    }
}
