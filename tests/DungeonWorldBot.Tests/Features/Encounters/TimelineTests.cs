using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Encounters;

public sealed class TimelineTests : IDisposable
{
    private static readonly Snowflake GameChat = DiscordSnowflake.New(222);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static TimelineService Timeline(DungeonWorldContext context, EncounterService encounters) =>
        new(new FightLogService(context, new RollStatsService(context, GameChatConfig()), encounters));

    private static IOptions<BotSettings> GameChatConfig() => Options.Create(new BotSettings { GameChatChannelID = GameChat.Value });

    private static Character Hero(ulong id, string name) => new() { ID = DiscordSnowflake.New(id), Name = name, Status = Status.Alive };

    [Fact]
    public async Task HasEverythingNewestFirst_EvenFromTheFallen()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var encounter = (await encounters.StartAsync("Ambush"))!;
        var aria = Hero(1, "Aria");
        var bram = Hero(2, "Bram");
        var goblin = new Enemy { Name = "Goblin", CurrentHP = 0, MaxHP = 3 };
        await encounters.AddEnemyAsync(encounter, goblin);

        var logged = Snowflake.CreateTimestampSnowflake(DateTimeOffset.UtcNow.AddSeconds(2), Constants.DiscordEpoch);
        var plain = Snowflake.CreateTimestampSnowflake(DateTimeOffset.UtcNow.AddSeconds(3), Constants.DiscordEpoch);
        context.Rolls.Add(new RollRecord { MessageID = logged, ChannelID = GameChat, UserID = aria.ID, First = 4, Second = 4, Total = 9, Move = "Hack and Slash" });
        context.Rolls.Add(new RollRecord { MessageID = plain, ChannelID = GameChat, UserID = bram.ID, First = 1, Second = 2, Total = 3, Move = "Defy Danger" });
        await context.SaveChangesAsync();
        await encounters.LogAsync(new EncounterAction
        {
            Actor = goblin.Name, EnemyID = goblin.ID, Action = "Bite", Roll = "d6 = 4", Target = "Aria", Result = "💥 3 damage",
            At = DateTimeOffset.UtcNow.AddSeconds(1),
        });
        await encounters.LogAsync(new EncounterAction
        {
            Actor = aria.Name, CharacterID = aria.ID, MessageID = logged, Action = "Hack and Slash", Target = "Goblin", Result = "💥 4 damage",
        });

        var timeline = await Timeline(context, encounters).GetAsync(encounter, [aria, bram], take: 10);

        // The defeated goblin's bite stays, and Aria's logged roll shows once, at the time she rolled it
        Assert.Equal(["Bram", "Aria", "Goblin"], timeline.Entries.Select(e => e.Actor));
        Assert.True(timeline.Entries[0].Action.Missed);
        Assert.Equal("Goblin", timeline.Entries[1].Action.Target);
        Assert.Equal("Bite · d6 = 4 → Aria · 💥 3 damage", timeline.Entries[2].Action.Summary);
        Assert.Equal(3, timeline.Total);
        Assert.False(timeline.HasMore);
    }

    [Fact]
    public async Task ChangedRolls_ShowWhyTheyWereChanged()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var encounter = (await encounters.StartAsync("Ambush"))!;
        var aria = Hero(1, "Aria");
        var bram = Hero(2, "Bram");

        var logged = Snowflake.CreateTimestampSnowflake(DateTimeOffset.UtcNow.AddSeconds(1), Constants.DiscordEpoch);
        var plain = Snowflake.CreateTimestampSnowflake(DateTimeOffset.UtcNow.AddSeconds(2), Constants.DiscordEpoch);
        context.Rolls.Add(new RollRecord { MessageID = logged, ChannelID = GameChat, UserID = aria.ID, First = 1, Second = 2, Total = 3, Move = "Hack and Slash", ChangedOutcome = MoveOutcome.StrongHit, ChangedReason = "Blessing of Luna" });
        context.Rolls.Add(new RollRecord { MessageID = plain, ChannelID = GameChat, UserID = bram.ID, First = 1, Second = 2, Total = 3, Move = "Defy Danger", ChangedOutcome = MoveOutcome.WeakHit, ChangedReason = "Lucky charm" });
        await context.SaveChangesAsync();
        await encounters.LogAsync(new EncounterAction { Actor = aria.Name, CharacterID = aria.ID, MessageID = logged, Action = "Hack and Slash", Roll = "3 · Strong Hit (10+)" });

        var timeline = await Timeline(context, encounters).GetAsync(encounter, [aria, bram], take: 10);

        Assert.Equal(["Lucky charm", "Blessing of Luna"], timeline.Entries.Select(e => e.Action.ChangedReason));
        Assert.All(timeline.Entries, e => Assert.False(e.Action.Missed));
        Assert.Equal("Defy Danger · 3 · Weak Hit (7-9) (changed: Lucky charm)", timeline.Entries[0].Action.Summary);
    }

    [Fact]
    public async Task ComesAPageAtATime_OlderAfterTheOldestShown()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var encounter = (await encounters.StartAsync("Ambush"))!;
        var start = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 5; i++)
            await encounters.LogAsync(new EncounterAction { Actor = $"Goblin {i}", Action = "Attack", At = start.AddSeconds(i) });

        var service = Timeline(context, encounters);
        var latest = await service.GetAsync(encounter, [], take: 2);
        var older = await service.GetAsync(encounter, [], take: 2, before: latest.Entries[^1].Action.At);
        var oldest = await service.GetAsync(encounter, [], take: 2, before: older.Entries[^1].Action.At);

        Assert.Equal(["Goblin 5", "Goblin 4"], latest.Entries.Select(e => e.Actor));
        Assert.True(latest.HasMore);
        Assert.Equal(["Goblin 3", "Goblin 2"], older.Entries.Select(e => e.Actor));
        Assert.True(older.HasMore);
        Assert.Equal(["Goblin 1"], oldest.Entries.Select(e => e.Actor));
        Assert.False(oldest.HasMore);
        Assert.Equal(5, oldest.Total);
    }
}
