using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Tests.Features.Encounters;

/// <summary>
/// The bot and the dashboard saving the same rows at the same moment, each with its own context.
/// </summary>
public sealed class SharedSaveTests : IDisposable
{
    private static readonly SceneLink Scene = new("abc", "The Ford", null);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task TwoHitsOnTheSameEnemy_TheLaterSaveIsRefused_NotLost()
    {
        int enemyID;
        await using (var setup = _db.CreateContext())
        {
            var encounter = (await new EncounterService(setup, TimeProvider.System).StartAsync("Ambush"))!;
            var enemy = new Enemy { Name = "Goblin", CurrentHP = 10, MaxHP = 10 };
            await new EncounterService(setup, TimeProvider.System).AddEnemyAsync(encounter, enemy);
            enemyID = enemy.ID;
        }

        await using var bot = _db.CreateContext();
        await using var dashboard = _db.CreateContext();
        var botsEnemy = await bot.Enemies.SingleAsync(e => e.ID == enemyID);
        var dashboardsEnemy = await dashboard.Enemies.SingleAsync(e => e.ID == enemyID);

        await new EncounterService(bot, TimeProvider.System).DamageAsync(botsEnemy, 3);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new EncounterService(dashboard, TimeProvider.System).DamageAsync(dashboardsEnemy, 4));

        // Tried again from the fresh row, both hits count
        await using var retry = _db.CreateContext();
        await new EncounterService(retry, TimeProvider.System).DamageAsync(await retry.Enemies.SingleAsync(e => e.ID == enemyID), 4);

        await using var check = _db.CreateContext();
        var saved = await check.Enemies.SingleAsync(e => e.ID == enemyID);
        Assert.Equal(3, saved.CurrentHP);
    }

    [Fact]
    public async Task AScene_HasOneFightGoingOn_EvenWhenTwoStartAtOnce()
    {
        await using var bot = _db.CreateContext();
        Assert.NotNull(await new EncounterService(bot, TimeProvider.System).StartAsync("Ambush", Scene));

        // Past the service's own check, as when the dashboard checked before the bot's fight was saved
        await using var dashboard = _db.CreateContext();
        dashboard.Encounters.Add(new Encounter { Name = "Second Wave", SceneID = Scene.ID, StartedAt = DateTimeOffset.UtcNow });
        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => dashboard.SaveChangesAsync());
        Assert.True(refused.IsUniqueViolation());
    }

    [Fact]
    public async Task AScene_CanHaveANewFight_OnceTheLastHasEnded()
    {
        await using var context = _db.CreateContext();
        var service = new EncounterService(context, TimeProvider.System);
        await service.EndAsync((await service.StartAsync("Ambush", Scene))!);

        Assert.NotNull(await service.StartAsync("Second Wave", Scene));
    }

    [Fact]
    public async Task Times_AreComparedInTheDatabase()
    {
        var start = new DateTimeOffset(2026, 10, 24, 19, 0, 0, TimeSpan.Zero);
        await using (var setup = _db.CreateContext())
        {
            // Saved with an offset, read back as the same moment in UTC
            setup.Encounters.Add(new Encounter { Name = "Early", StartedAt = start.ToOffset(TimeSpan.FromHours(-7)), EndedAt = start });
            setup.Encounters.Add(new Encounter { Name = "Late", StartedAt = start.AddHours(2), EndedAt = start.AddHours(2) });
            await setup.SaveChangesAsync();
        }

        await using var context = _db.CreateContext();
        var after = await context.Encounters.Where(e => e.StartedAt > start.AddHours(1)).Select(e => e.Name).ToListAsync();
        var newestFirst = await context.Encounters.OrderByDescending(e => e.StartedAt).Select(e => e.StartedAt).ToListAsync();

        Assert.Equal(["Late"], after);
        Assert.Equal([start.AddHours(2), start], newestFirst);
        Assert.All(newestFirst, at => Assert.Equal(TimeSpan.Zero, at.Offset));
    }
}
