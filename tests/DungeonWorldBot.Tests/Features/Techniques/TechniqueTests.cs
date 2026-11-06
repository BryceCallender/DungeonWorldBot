using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Techniques;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Techniques;

public sealed class TechniqueTests : IDisposable
{
    private static readonly Snowflake UserId = new(99);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static async Task<Character> AddChannelerAsync(DungeonWorldContext context, params string[] moves)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Ember",
            Class = new Class { Type = ClassType.Channeler },
            Health = new Health { CurrentHP = 18, MaxHP = 18 },
            Inventory = new Inventory(),
            Stats = [new Stat { StatType = StatType.Con, Value = 18 }],
            Abilities = new[] { TechniqueRules.Channel }.Concat(moves).Select(m => new Ability { Name = m }).ToList(),
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    private static TechniqueService ServiceFor(DungeonWorldContext context) => new(context, new ArmorService(context));

    private static MoveResolver ResolverFor(DungeonWorldContext context, int everyDie)
    {
        var dice = new DiceRoller(new ConstantDieRoller(everyDie));
        var characters = new CharacterService(context);
        var damage = new DamageService(characters, dice, new GearService(new AbilityService(context), new ResourceService(context), dice), new ArmorService(context));
        return new MoveResolver(dice, damage, new EncounterService(context, TimeProvider.System));
    }

    [Fact]
    public async Task DevelopedTechniques_AreSavedAndFoundByName()
    {
        await using var context = CreateContext();
        var character = await AddChannelerAsync(context);

        await ServiceFor(context).DevelopAsync(character, " Ember Lance ", TechniqueTag.Projectile | TechniqueTag.Piercing, signature: false);

        await using var reloaded = CreateContext();
        var saved = await new CharacterService(reloaded).GetCharacterFromUserAsync(UserId);

        var lance = TechniqueRules.Find(saved!, "ember lance");
        Assert.NotNull(lance);
        Assert.Equal("Ember Lance", lance.Name);
        Assert.Equal([TechniqueTag.Projectile, TechniqueTag.Piercing], TechniqueRules.TagsOf(lance.Tags));
    }

    [Fact]
    public async Task Techniques_NeedExactlyTheirTags_AndMovesForLockedOnes()
    {
        await using var context = CreateContext();
        var character = await AddChannelerAsync(context);
        var plain = new Technique();

        Assert.Null(TechniqueRules.WhyCannotHave(character, plain, TechniqueTag.Blast | TechniqueTag.Forceful));
        Assert.NotNull(TechniqueRules.WhyCannotHave(character, plain, TechniqueTag.Blast));
        Assert.NotNull(TechniqueRules.WhyCannotHave(character, plain, TechniqueTag.Blast | TechniqueTag.Forceful | TechniqueTag.Hazard));
        Assert.NotNull(TechniqueRules.WhyCannotHave(character, plain, TechniqueTag.Teleport | TechniqueTag.Barrier));

        character.Abilities.Add(new Ability { Name = TechniqueRules.Teleport });
        Assert.Null(TechniqueRules.WhyCannotHave(character, plain, TechniqueTag.Teleport | TechniqueTag.Barrier));
        Assert.Null(TechniqueRules.WhyCannotHave(character, new Technique { IsSignature = true }, TechniqueTag.Blast | TechniqueTag.Forceful | TechniqueTag.Hazard));
    }

    [Fact]
    public async Task SignatureTechnique_AddsASlot()
    {
        await using var context = CreateContext();
        var character = await AddChannelerAsync(context);
        var service = ServiceFor(context);
        foreach (var name in new[] { "One", "Two", "Three" })
            await service.DevelopAsync(character, name, TechniqueTag.Blast | TechniqueTag.Hazard, signature: false);

        Assert.NotNull(TechniqueRules.WhyCannotDevelop(character, "Four", signature: false));
        Assert.NotNull(TechniqueRules.WhyCannotDevelop(character, "Four", signature: true));

        character.Abilities.Add(new Ability { Name = TechniqueRules.SignatureTechnique });
        Assert.Equal(4, TechniqueRules.MaxTechniques(character));
        Assert.Null(TechniqueRules.WhyCannotDevelop(character, "Four", signature: true));
        Assert.NotNull(TechniqueRules.WhyCannotDevelop(character, "Four", signature: false));
        Assert.NotNull(TechniqueRules.WhyCannotDevelop(character, "one", signature: false));
    }

    [Fact]
    public void Damage_ComesFromBlastOrProjectile_ByRange()
    {
        Assert.Equal(new TechniqueDamage("1d8", TechniqueTag.Blast, 2), TechniqueRules.DamageOf(TechniqueTag.Blast | TechniqueTag.Piercing, null));
        Assert.Equal(new TechniqueDamage("1d6", TechniqueTag.Projectile, 0), TechniqueRules.DamageOf(TechniqueTag.Projectile | TechniqueTag.Hazard, null));
        Assert.Null(TechniqueRules.DamageOf(TechniqueTag.Barrier | TechniqueTag.Hazard, null));

        var both = TechniqueTag.Blast | TechniqueTag.Projectile;
        Assert.True(TechniqueRules.NeedsRange(both, null));
        Assert.Equal("1d6", TechniqueRules.DamageOf(both, TechniqueRange.Near)!.Dice);
        Assert.Equal("1d8", TechniqueRules.DamageOf(both, TechniqueRange.Close)!.Dice);
    }

    [Fact]
    public async Task Barrier_LastsUntilTheNextChannel_AndArmorOfTheElementsDoublesIt()
    {
        await using var context = CreateContext();
        var character = await AddChannelerAsync(context, TechniqueRules.ArmorOfTheElements);
        var service = ServiceFor(context);

        Assert.Equal(2, await service.RaiseBarrierAsync(character));
        Assert.Contains(character.ArmorBonuses, b => b.Source == TechniqueRules.BarrierSource && b.Amount == 2);

        var dropped = await service.DropBarrierAsync(character);
        Assert.Single(dropped);
        Assert.DoesNotContain(character.ArmorBonuses, b => b.Source == TechniqueRules.BarrierSource);
    }

    [Fact]
    public async Task ChannelingDamage_PiercesArmor_AndThreadTheNeedleCanBacklash()
    {
        await using var context = CreateContext();
        var character = await AddChannelerAsync(context, TechniqueRules.ThreadTheNeedle);
        var encounters = new EncounterService(context, TimeProvider.System);
        var fight = (await encounters.StartAsync("Ambush"))!;
        var goblin = new Enemy { Name = "Goblin", CurrentHP = 10, MaxHP = 10, Armor = 2, Damage = "d6" };
        await encounters.AddEnemyAsync(fight, goblin);

        // Every die lands on 2: 2d6+3 is a 7, Blast's d8 is 2 and the Thread the Needle d4 is 2, which hurts
        var channel = TechniqueRules.ChannelMove.Roll! with { RollsDamage = true };
        var input = MoveInput.ForStat(character, StatType.Con, 0, target: "Goblin")! with { Damage = new MoveDamage("1d8", "Blast", Piercing: 2, RiskDice: 1) };
        var resolution = await ResolverFor(context, 2).ResolveAsync(character, channel, input);

        Assert.Equal(MoveOutcome.WeakHit, resolution.Outcome);
        Assert.Equal(4, resolution.Dealt!.Taken);
        Assert.Equal(6, goblin.CurrentHP);
        Assert.Equal(16, character.Health.CurrentHP);
    }
}
