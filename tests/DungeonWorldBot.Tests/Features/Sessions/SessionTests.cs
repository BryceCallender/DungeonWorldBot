using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Sessions;
using Microsoft.Extensions.Configuration;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Sessions;

public sealed class SessionTests : IDisposable
{
    private static readonly Snowflake GameChat = new(222);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static SessionService Service(DungeonWorldContext context) =>
        new(context, new RollStatsService(context, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GameChatChannelId"] = GameChat.Value.ToString() })
            .Build()));

    // A roll made at a moment, as its message ID says
    private static RollRecord Roll(DateTimeOffset at, ulong player, int first, int second, int total, Snowflake? channel = null) => new()
    {
        MessageID = Snowflake.CreateTimestampSnowflake(at, Constants.DiscordEpoch),
        ChannelID = channel ?? GameChat,
        UserID = new Snowflake(player),
        First = first,
        Second = second,
        Total = total,
    };

    [Fact]
    public async Task OneSessionGoesOnAtATime_NumberedInOrder()
    {
        await using var context = _db.CreateContext();
        var sessions = Service(context);

        Assert.Null(await sessions.GetCurrentAsync());
        Assert.Null(await sessions.EndAsync());

        var first = (await sessions.StartAsync("  Into the Sunken Abbey "))!;
        Assert.Equal("Into the Sunken Abbey", first.Title);
        Assert.Null(await sessions.StartAsync());
        Assert.Equal(first.ID, (await sessions.GetCurrentAsync())?.ID);

        var ended = (await sessions.EndAsync())!;
        Assert.NotNull(ended.EndedAt);
        Assert.Null(await sessions.GetCurrentAsync());

        var second = (await sessions.StartAsync(" "))!;
        Assert.Null(second.Title);
        Assert.Equal(1, await sessions.NumberOfAsync(first));
        Assert.Equal(2, await sessions.NumberOfAsync(second));
        Assert.Equal([first.ID, second.ID], (await sessions.GetAllAsync()).Select(s => s.ID));
        Assert.Null(await sessions.GetPreviousAsync(first));
        Assert.Equal(first.ID, (await sessions.GetPreviousAsync(second))?.ID);
    }

    [Fact]
    public async Task Recap_CountsOnlyGameChatRollsWhileItWasOn()
    {
        await using var context = _db.CreateContext();
        var start = new DateTimeOffset(2026, 10, 24, 19, 0, 0, TimeSpan.Zero);
        var session = new GameSession { StartedAt = start, EndedAt = start.AddHours(3) };
        context.Sessions.Add(session);
        context.Rolls.AddRange(
            Roll(start.AddMinutes(-30), 1, 6, 6, 12),               // before it started
            Roll(start.AddMinutes(10), 1, 6, 6, 13),
            Roll(start.AddMinutes(20), 1, 1, 1, 2),
            Roll(start.AddMinutes(30), 2, 4, 3, 8),
            Roll(start.AddMinutes(40), 2, 5, 5, 10, new Snowflake(333)), // another channel
            Roll(start.AddHours(4), 2, 2, 2, 4));                   // after it ended
        context.Encounters.Add(new Encounter { Name = "Ambush", StartedAt = start.AddHours(1) });
        await context.SaveChangesAsync();

        var recap = await Service(context).RecapAsync(session);

        Assert.Equal(3, recap.Rolls);
        Assert.Equal(2, recap.Hits);
        Assert.Equal(1, recap.Misses);
        Assert.Equal(1, recap.Natural12s);
        Assert.Equal(1, recap.Natural2s);
        Assert.Equal(["Ambush"], recap.Fights);
        Assert.Equal([new PlayerSessionRolls(new Snowflake(1), 2, 1), new PlayerSessionRolls(new Snowflake(2), 1, 0)], recap.Players);
    }

    [Fact]
    public async Task Backfill_GuessesPastNightsFromGameChatRolls_AndRenumbersByStart()
    {
        await using var context = _db.CreateContext();
        var night1 = new DateTimeOffset(2026, 9, 5, 19, 0, 0, TimeSpan.Zero);
        var night2 = night1.AddDays(7);
        var marked = night2.AddDays(7);
        context.Sessions.Add(new GameSession { Title = "Into the Sunken Abbey", StartedAt = marked, EndedAt = marked.AddHours(3) });
        // A night that runs past midnight, a stray roll days later, a night in another channel, and the marked night's rolls
        context.Rolls.AddRange(Enumerable.Range(0, 6).Select(i => Roll(night1.AddHours(i), 1, 3, 4, 7)));
        context.Rolls.Add(Roll(night1.AddDays(3), 1, 3, 4, 7));
        context.Rolls.AddRange(Enumerable.Range(0, 5).Select(i => Roll(night2.AddMinutes(i * 20), 2, 3, 4, 7)));
        context.Rolls.AddRange(Enumerable.Range(0, 5).Select(i => Roll(night2.AddDays(2).AddMinutes(i), 2, 3, 4, 7, new Snowflake(333))));
        context.Rolls.AddRange(Enumerable.Range(0, 5).Select(i => Roll(marked.AddMinutes(i * 20), 2, 3, 4, 7)));
        await context.SaveChangesAsync();

        var sessions = Service(context);
        var added = await sessions.BackfillAsync();

        Assert.Equal([(night1, night1.AddHours(5)), (night2, night2.AddMinutes(80))], added.Select(s => (s.StartedAt, s.EndedAt!.Value)));
        var all = await sessions.GetAllAsync();
        Assert.Equal([night1, night2, marked], all.Select(s => s.StartedAt));
        Assert.Equal("Into the Sunken Abbey", all[2].Title);
        Assert.Equal(3, await sessions.NumberOfAsync(all[2]));

        Assert.Empty(await Service(context).BackfillAsync());
        Assert.Equal(3, (await sessions.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Rename_TidiesTheTitle_OrTakesItAway()
    {
        await using var context = _db.CreateContext();
        var sessions = Service(context);
        var session = (await sessions.StartAsync())!;

        await sessions.RenameAsync(session, "  The Ford ");
        Assert.Equal("The Ford", (await sessions.GetCurrentAsync())?.Title);

        await sessions.RenameAsync(session, " ");
        Assert.Null((await sessions.GetCurrentAsync())?.Title);
    }
}
