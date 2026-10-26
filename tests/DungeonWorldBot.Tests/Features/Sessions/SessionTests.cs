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
        Assert.Equal(1, recap.Fights);
        Assert.Equal([new PlayerSessionRolls(new Snowflake(1), 2, 1), new PlayerSessionRolls(new Snowflake(2), 1, 0)], recap.Players);
    }
}
