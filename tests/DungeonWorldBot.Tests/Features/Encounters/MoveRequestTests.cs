using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Encounters;

public sealed class MoveRequestTests : IDisposable
{
    private static readonly Snowflake UserId = new(77);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    // Every die lands on 5: 2d6 is 10, and a paladin's d10 is 5
    private static MoveRequestProcessor Processor(DungeonWorldContext context)
    {
        var dice = new DiceRoller(new ConstantDieRoller(5));
        var characters = new CharacterService(context);
        var encounters = new EncounterService(context);
        var npcs = new NpcService(context);
        var damage = new DamageService(characters, dice, new GearService(new AbilityService(context), new ResourceService(context), dice), new ArmorService(context));
        return new MoveRequestProcessor(
            context,
            characters,
            encounters,
            new MoveResolver(dice, damage, encounters),
            new EnemyAttackResolver(dice, damage, npcs));
    }

    private static async Task<(Encounter Fight, Enemy Goblin)> SetupAsync(DungeonWorldContext context)
    {
        await new CharacterService(context).AddCharacterAsync(new Character
        {
            ID = UserId,
            Name = "Arthur",
            Class = new Class { Type = ClassType.Paladin },
            Health = new Health { CurrentHP = 20, MaxHP = 20 },
            Inventory = new Inventory(),
            Stats = [new Stat { StatType = StatType.Str, Value = 16 }],
            ArmorRating = 1,
        });

        var encounters = new EncounterService(context);
        var fight = (await encounters.StartAsync("Ambush"))!;
        var goblin = new Enemy { Name = "Goblin", CurrentHP = 6, MaxHP = 6, Armor = 1, Damage = "d6" };
        await encounters.AddEnemyAsync(fight, goblin);
        return (fight, goblin);
    }

    [Fact]
    public async Task HackAndSlash_RollsByTheSameRulesAsMove_AndHurtsTheTarget()
    {
        await using var context = _db.CreateContext();
        var (fight, goblin) = await SetupAsync(context);

        var request = new MoveRequest
        {
            EncounterID = fight.ID,
            Kind = MoveRequestKind.HackAndSlash,
            CharacterID = UserId,
            TargetEnemyID = goblin.ID,
        };
        var processed = await Processor(context).ProcessAsync(request);

        var move = Assert.IsType<ProcessedMove>(processed.Move);
        // 10 + STR 2 is a strong hit; d10 = 5 less the goblin's 1 armor
        Assert.Equal("Strong hit · -4", move.Summary);
        Assert.Equal(2, goblin.CurrentHP);
        Assert.Equal(UserId, move.Roller);
        Assert.Contains("Arthur: Hack and Slash", move.Embed.Title.Value);

        var action = await context.EncounterActions.SingleAsync();
        Assert.Equal(action.ID, request.ActionID);
        Assert.Equal(("Arthur", "Goblin", "💥 4 damage"), (action.Actor, action.Target, action.Result));
    }

    [Fact]
    public async Task EnemyAttack_HitsTheCharacterThroughTheirArmor()
    {
        await using var context = _db.CreateContext();
        var (fight, goblin) = await SetupAsync(context);

        var processed = await Processor(context).ProcessAsync(new MoveRequest
        {
            EncounterID = fight.ID,
            Kind = MoveRequestKind.EnemyAttack,
            EnemyID = goblin.ID,
            TargetCharacterID = UserId,
            Move = "Stab",
        });

        var move = Assert.IsType<ProcessedMove>(processed.Move);
        Assert.Equal("-4", move.Summary);
        Assert.Null(move.Roller);
        Assert.Equal(16, (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!.Health.CurrentHP);
        Assert.Equal("Stab", (await context.EncounterActions.SingleAsync()).Action);
    }

    [Fact]
    public async Task Moves_InAFightThatsOver_OrAtSomeoneElsesEnemy_AreRefused()
    {
        await using var context = _db.CreateContext();
        var (fight, goblin) = await SetupAsync(context);
        var encounters = new EncounterService(context);
        var other = (await encounters.StartAsync("Elsewhere"))!;
        var orc = new Enemy { Name = "Orc", CurrentHP = 8, MaxHP = 8 };
        await encounters.AddEnemyAsync(other, orc);

        var elsewhere = await Processor(context).ProcessAsync(new MoveRequest
        {
            EncounterID = fight.ID,
            Kind = MoveRequestKind.HackAndSlash,
            CharacterID = UserId,
            TargetEnemyID = orc.ID,
        });
        Assert.Null(elsewhere.Move);
        Assert.Equal(8, orc.CurrentHP);

        await encounters.EndAsync(fight, announced: true);
        var over = await Processor(context).ProcessAsync(new MoveRequest
        {
            EncounterID = fight.ID,
            Kind = MoveRequestKind.HackAndSlash,
            CharacterID = UserId,
            TargetEnemyID = goblin.ID,
        });
        Assert.Equal("The fight is over.", over.Error);
        Assert.Equal(6, goblin.CurrentHP);
    }
}
