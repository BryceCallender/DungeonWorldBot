using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Npcs;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Encounters;

public sealed class EncounterTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static MonsterTemplate Goblin() => new()
    {
        Name = "Goblin",
        MaxHP = 4,
        Armor = 1,
        Damage = "d6",
        Tags = "Horde, Small",
        Instinct = "To overwhelm",
        Moves = "Charge!; Call more goblins",
    };

    private static async Task<(EncounterService Service, Encounter Encounter)> FightAsync(DungeonWorldContext context)
    {
        var service = new EncounterService(context);
        var encounter = (await service.StartAsync("Ambush"))!;
        return (service, encounter);
    }

    [Fact]
    public async Task SeveralFights_GoAtOnce_EachWithItsOwnNameAndScene()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var ford = new SceneLink("dEsXAr", "The ford", null);

        Assert.Empty(await service.GetRunningAsync());
        var ambush = (await service.StartAsync("Ambush", ford))!;
        Assert.NotNull(await service.StartAsync("Second fight"));

        // Names are unique among the fights going on, and a scene has one at a time
        Assert.Null(await service.StartAsync("AMBUSH"));
        Assert.Null(await service.StartAsync("Third fight", ford));
        Assert.Equal(["Ambush", "Second fight"], (await service.GetRunningAsync()).Select(e => e.Name));
        Assert.Equal("Ambush", (await service.GetOnSceneAsync("dEsXAr"))?.Name);
        Assert.Equal("Second fight", (await service.FindRunningAsync(" second FIGHT "))?.Name);

        await service.EndAsync(ambush);
        Assert.Equal("Second fight", Assert.Single(await service.GetRunningAsync()).Name);
        Assert.Null(await service.GetOnSceneAsync("dEsXAr"));
        Assert.NotNull(await service.StartAsync("Ambush", ford));
    }

    [Fact]
    public async Task EnemyNames_AreNumberedAcrossTheFightsGoingOn()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var first = (await service.StartAsync("Ambush"))!;
        var second = (await service.StartAsync("Raid"))!;

        await service.SpawnAsync(first, Goblin(), 2);
        var more = await service.SpawnAsync(second, Goblin());

        Assert.Equal(["Goblin 3"], more.Select(e => e.Name));
        Assert.Equal(second.ID, (await service.FindEnemyAsync("goblin 3"))?.EncounterID);
    }

    [Fact]
    public async Task Actions_GoToTheFightTheyBelongTo()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var aria = DiscordSnowflake.New(1);
        var bram = DiscordSnowflake.New(2);
        var ambush = (await service.StartAsync("Ambush", roster: new EncounterRoster([aria], [])))!;
        var raid = (await service.StartAsync("Raid", roster: new EncounterRoster([bram], [])))!;
        var ogre = new Enemy { Name = "Ogre", MaxHP = 10, CurrentHP = 10 };
        await service.AddEnemyAsync(raid, ogre);

        // Their own fight, their target's fight, and an enemy's own fight
        await service.LogAsync(new EncounterAction { Actor = "Aria", CharacterID = aria, Action = "Defy Danger" });
        await service.LogAsync(new EncounterAction { Actor = "Aria", CharacterID = aria, Action = "Volley", Target = "ogre" });
        await service.LogAsync(new EncounterAction { Actor = "Ogre", EnemyID = ogre.ID, Action = "Smash" });

        Assert.Equal(["Defy Danger"], (await service.GetActionsAsync(ambush)).Select(a => a.Action));
        Assert.Equal(["Volley", "Smash"], (await service.GetActionsAsync(raid)).Select(a => a.Action));
    }

    [Fact]
    public async Task Targets_AreTheEnemiesInTheActorsFight()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var npcs = new NpcService(context);
        var grub = new Npc { Name = "Grub", CurrentHP = 6, MaxHP = 6 };
        await npcs.AddAsync(grub);
        var ambush = (await service.StartAsync("Ambush", roster: new EncounterRoster([], [])))!;
        var raid = (await service.StartAsync("Raid", roster: new EncounterRoster([], [grub.ID])))!;
        await service.AddEnemyAsync(ambush, new Enemy { Name = "Goblin", MaxHP = 3, CurrentHP = 3 });
        await service.AddEnemyAsync(raid, new Enemy { Name = "Ogre", MaxHP = 10, CurrentHP = 10 });
        var targets = new TargetAutocomplete(service, new CharacterService(context), npcs);

        // Grub is in the raid, so only its enemies; with nobody to go on, every fight's, saying which
        var grubs = await targets.GetSuggestionsAsync([Option("roll", [Option("name", value: "grub")])], "");
        Assert.Equal(["Ogre"], grubs.Select(c => c.Value.AsT0));
        var anyone = await targets.GetSuggestionsAsync([], "");
        Assert.Equal(["Goblin · Unhurt · Ambush", "Ogre · Unhurt · Raid"], anyone.Select(c => c.Name));
    }

    private static ApplicationCommandInteractionDataOption Option(
        string name, IReadOnlyList<IApplicationCommandInteractionDataOption>? options = null, string? value = null) =>
        new(name, ApplicationCommandOptionType.String,
            value is null ? default(Optional<OneOf.OneOf<string, long, bool, Snowflake, double>>) : new Optional<OneOf.OneOf<string, long, bool, Snowflake, double>>(value),
            options is null ? default(Optional<IReadOnlyList<IApplicationCommandInteractionDataOption>>) : new Optional<IReadOnlyList<IApplicationCommandInteractionDataOption>>(options));

    [Fact]
    public async Task Picks_AreInOneFightAtATime()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var aria = DiscordSnowflake.New(1);
        var bram = DiscordSnowflake.New(2);
        var grub = new Npc { Name = "Grub", CurrentHP = 6, MaxHP = 6 };
        await new NpcService(context).AddAsync(grub);
        var ambush = (await service.StartAsync("Ambush", roster: new EncounterRoster([aria], [grub.ID])))!;

        // Aria and Grub are taken, so the raid only gets Bram
        var raid = (await service.StartAsync("Raid", roster: new EncounterRoster([aria, bram], [grub.ID])))!;
        Assert.Equal([bram], raid.Participants.Select(p => p.CharacterID!.Value));
        var taken = await service.GetTakenAsync();
        Assert.Equal([aria, bram], taken.Characters.OrderBy(id => id.Value));
        Assert.Equal([grub.ID], taken.Npcs);

        // Free again once their fight ends
        await service.EndAsync(ambush);
        Assert.Equal([bram], (await service.GetTakenAsync()).Characters);
    }

    [Fact]
    public async Task EndingAFight_OnlyTakesArmorFromWhoWasInIt()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var aria = DiscordSnowflake.New(1);
        var bram = DiscordSnowflake.New(2);
        var cass = DiscordSnowflake.New(3);
        context.Characters.Add(new Character { ID = aria, Name = "Aria", Stats = [] });
        context.Characters.Add(new Character { ID = bram, Name = "Bram", Stats = [] });
        context.Characters.Add(new Character { ID = cass, Name = "Cass", Stats = [] });
        await context.SaveChangesAsync();

        var picked = (await service.StartAsync("Ambush", roster: new EncounterRoster([aria], [])))!;
        var open = (await service.StartAsync("Brawl"))!;

        Assert.Equal([aria], await service.LeavingAsync(picked));
        // Open to everyone, so everyone but those picked for a fight still going
        Assert.Equal([bram, cass], (await service.LeavingAsync(open)).OrderBy(id => id.Value));
    }

    [Fact]
    public async Task Spawning_NumbersAfterThoseAlreadyThere()
    {
        await using var context = CreateContext();
        var (service, encounter) = await FightAsync(context);
        var goblin = Goblin();
        await service.SaveMonsterAsync(goblin);

        var first = await service.SpawnAsync(encounter, goblin, 3);
        Assert.Equal(["Goblin 1", "Goblin 2", "Goblin 3"], first.Select(e => e.Name));

        var more = await service.SpawnAsync(encounter, goblin);
        Assert.Equal("Goblin 4", Assert.Single(more).Name);

        var spawned = more[0];
        Assert.Equal(4, spawned.CurrentHP);
        Assert.Equal(1, spawned.Armor);
        Assert.Equal("To overwhelm", spawned.Instinct);
    }

    [Theory]
    [InlineData(new string[0], 1, new[] { "Goblin" })]
    [InlineData(new string[0], 2, new[] { "Goblin 1", "Goblin 2" })]
    [InlineData(new[] { "Goblin" }, 1, new[] { "Goblin 2" })]
    [InlineData(new[] { "goblin 7", "Goblin Chief" }, 1, new[] { "Goblin 8" })]
    public void SpawnNames_CountPlainNamesAsTheFirst(string[] existing, int count, string[] expected)
    {
        Assert.Equal(expected, EncounterService.SpawnNames(existing, "Goblin", count));
    }

    [Theory]
    [InlineData(DamageType.Physical, 0, 3, 1)]
    [InlineData(DamageType.Physical, 1, 3, 0)]
    [InlineData(DamageType.Magical, 0, 3, 0)]
    [InlineData(DamageType.True, 0, 3, 0)]
    public async Task Damage_IsReducedByArmorOnlyWhenPhysical(DamageType type, int piercing, int rolled, int blocked)
    {
        await using var context = CreateContext();
        var (service, encounter) = await FightAsync(context);
        var goblin = Assert.Single(await service.SpawnAsync(encounter, Goblin()));

        var hit = await service.DamageAsync(goblin, rolled, type, piercing);

        Assert.Equal(blocked, hit.Blocked);
        Assert.Equal(rolled - blocked, hit.Taken);
        Assert.Equal(4 - hit.Taken, goblin.CurrentHP);
    }

    [Fact]
    public async Task Hp_StaysBetweenZeroAndMax()
    {
        await using var context = CreateContext();
        var (service, encounter) = await FightAsync(context);
        var goblin = Assert.Single(await service.SpawnAsync(encounter, Goblin()));

        await service.DamageAsync(goblin, 50);
        Assert.Equal(0, goblin.CurrentHP);
        Assert.Equal(EnemyCondition.Defeated, goblin.Condition);

        await service.HealAsync(goblin, 100);
        Assert.Equal(4, goblin.CurrentHP);

        goblin.MaxHP = 2;
        await service.SaveAsync(goblin);
        Assert.Equal(2, goblin.CurrentHP);
    }

    [Theory]
    [InlineData(10, EnemyCondition.Unhurt)]
    [InlineData(9, EnemyCondition.Hurt)]
    [InlineData(5, EnemyCondition.Bloodied)]
    [InlineData(3, EnemyCondition.Bloodied)]
    [InlineData(2, EnemyCondition.NearDeath)]
    [InlineData(0, EnemyCondition.Defeated)]
    public void Condition_FollowsHp(int hp, EnemyCondition expected)
    {
        Assert.Equal(expected, EnemyConditions.Of(hp, 10));
    }

    [Fact]
    public async Task Enemies_AreFoundInTheCurrentFightWhateverTheCase()
    {
        await using (var context = CreateContext())
        {
            var (service, encounter) = await FightAsync(context);
            await service.SpawnAsync(encounter, Goblin(), 2);
        }

        await using (var context = CreateContext())
        {
            var service = new EncounterService(context);
            Assert.Equal("Goblin 2", (await service.FindEnemyAsync("  gOBLIN 2 "))?.Name);

            await service.EndAsync((await service.FindRunningAsync("Ambush"))!);
            Assert.Null(await service.FindEnemyAsync("Goblin 2"));
        }
    }

    [Fact]
    public async Task EnemyNames_AreUniqueInAFightButNotAcrossFights()
    {
        await using var context = CreateContext();
        var (service, encounter) = await FightAsync(context);
        await service.AddEnemyAsync(encounter, new Enemy { Name = "Ogre", MaxHP = 10, CurrentHP = 10 });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.AddEnemyAsync(encounter, new Enemy { Name = "ogre", MaxHP = 10, CurrentHP = 10 }));
        context.ChangeTracker.Clear();

        await service.EndAsync(encounter);
        var next = (await service.StartAsync("Rematch"))!;
        await service.AddEnemyAsync(next, new Enemy { Name = "Ogre", MaxHP = 10, CurrentHP = 10 });
        Assert.Equal(2, await context.Enemies.CountAsync());
    }

    [Fact]
    public async Task Monsters_AreUniqueWhateverTheCaseAndSaveInPlace()
    {
        await using (var context = CreateContext())
        {
            var service = new EncounterService(context);
            await service.SaveMonsterAsync(Goblin());
            await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveMonsterAsync(new MonsterTemplate { Name = "GOBLIN", MaxHP = 1 }));
        }

        await using (var context = CreateContext())
        {
            var service = new EncounterService(context);
            var goblin = (await service.FindMonsterAsync("goblin"))!;
            goblin.MaxHP = 6;
            await service.SaveMonsterAsync(goblin);

            var monster = Assert.Single(await service.GetMonstersAsync());
            Assert.Equal(6, monster.MaxHP);
        }
    }

    [Fact]
    public async Task DashboardFights_WaitToBeAnnounced_AndCommandFightsDont()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var scene = new SceneLink("dEsXAr", "The ford", "https://example.test/scene/dEsXAr");

        // Started on the dashboard: the bot announces it later
        var fight = (await service.StartAsync("Ambush", scene))!;
        Assert.Equal("dEsXAr", fight.SceneID);
        Assert.Equal("https://example.test/scene/dEsXAr", fight.SceneUrl);
        Assert.Equal(fight.ID, Assert.Single(await service.GetUnannouncedAsync()).ID);

        fight.AnnouncedAt = DateTimeOffset.UtcNow;
        await service.SaveAsync(fight);
        Assert.Empty(await service.GetUnannouncedAsync());

        // Ended on the dashboard: waits for its own announcement
        await service.EndAsync(fight);
        Assert.Equal(fight.ID, Assert.Single(await service.GetUnannouncedAsync()).ID);

        // Started and ended by commands, whose replies are the announcements
        var rematch = (await service.StartAsync("Rematch", announced: true))!;
        await service.EndAsync(rematch, announced: true);
        Assert.DoesNotContain(await service.GetUnannouncedAsync(), e => e.Name == "Rematch");
    }

    [Fact]
    public async Task EndingByCommand_CoversAStartNobodyAnnounced()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        var fight = (await service.StartAsync("Ambush"))!;

        await service.EndAsync(fight, announced: true);

        Assert.Empty(await service.GetUnannouncedAsync());
    }

    [Fact]
    public void Moves_SplitOnSemicolonsAndLines()
    {
        Assert.Equal(["Charge!", "Call more goblins", "Flee"], EnemySheet.SplitMoves("Charge!; Call more goblins\nFlee;"));
    }
}
