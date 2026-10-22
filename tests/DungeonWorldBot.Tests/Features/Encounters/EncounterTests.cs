using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;

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
    public async Task OnlyOneFight_GoesAtATime()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);

        Assert.Null(await service.GetActiveAsync());
        Assert.NotNull(await service.StartAsync("Ambush"));
        Assert.Null(await service.StartAsync("Second fight"));

        var ended = await service.EndAsync();
        Assert.Equal("Ambush", ended?.Name);
        Assert.Null(await service.GetActiveAsync());
        Assert.Null(await service.EndAsync());

        Assert.NotNull(await service.StartAsync("Second fight"));
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

            await service.EndAsync();
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

        await service.EndAsync();
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
        await service.EndAsync();
        Assert.Equal(fight.ID, Assert.Single(await service.GetUnannouncedAsync()).ID);

        // Started and ended by commands, whose replies are the announcements
        await service.StartAsync("Rematch", announced: true);
        await service.EndAsync(announced: true);
        Assert.DoesNotContain(await service.GetUnannouncedAsync(), e => e.Name == "Rematch");
    }

    [Fact]
    public async Task EndingByCommand_CoversAStartNobodyAnnounced()
    {
        await using var context = CreateContext();
        var service = new EncounterService(context);
        await service.StartAsync("Ambush");

        await service.EndAsync(announced: true);

        Assert.Empty(await service.GetUnannouncedAsync());
    }

    [Fact]
    public void Moves_SplitOnSemicolonsAndLines()
    {
        Assert.Equal(["Charge!", "Call more goblins", "Flee"], EnemySheet.SplitMoves("Charge!; Call more goblins\nFlee;"));
    }
}
