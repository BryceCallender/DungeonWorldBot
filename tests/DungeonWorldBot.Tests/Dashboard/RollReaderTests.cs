using DungeonWorldBot.Dashboard.Models.Rolls;
using DungeonWorldBot.Dashboard.Services.Rolls;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Dashboard;

public sealed class RollReaderTests : IDisposable
{
    private const ulong GameChat = 222;
    private const ulong Aria = 1;
    private const ulong Bram = 2;
    private const ulong Dm = 9;

    private static readonly DateTimeOffset Night = new(2026, 11, 1, 19, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _db = new();
    private readonly DungeonWorldContext _context;

    public RollReaderTests() => _context = _db.CreateContext();

    public void Dispose()
    {
        _context.Dispose();
        _db.Dispose();
    }

    private RollReader Reader(ulong gameChat = GameChat) =>
        new(_context, new RollStatsService(_context, Options.Create(new BotSettings { GameChatChannelID = gameChat })));

    /// <summary>
    /// A roll the given number of minutes into <see cref="Night"/>, with a message ID like Discord's for that moment.
    /// </summary>
    private void Roll(double minutes, ulong player, int first, int second, int modifier = 0, string? move = null,
        StatType? stat = null, ulong channel = GameChat) =>
        _context.Rolls.Add(new RollRecord
        {
            MessageID = Snowflake.CreateTimestampSnowflake(Night.AddMinutes(minutes), Constants.DiscordEpoch),
            ChannelID = new Snowflake(channel),
            UserID = new Snowflake(player),
            First = first,
            Second = second,
            Modifier = modifier,
            Total = first + second + modifier,
            Move = move,
            Stat = stat,
        });

    [Fact]
    public async Task WithoutAGameChat_NothingCounts()
    {
        Roll(0, Aria, 6, 6);
        await _context.SaveChangesAsync();
        var reader = Reader(gameChat: 0);

        Assert.Same(RollSpreads.None, await reader.GetSpreadsAsync());
        Assert.Empty((await reader.GetBreakdownAsync()).Moves);
        Assert.Empty(await reader.GetStreaksAsync());
        Assert.Null(await reader.GetAwardsAsync(Night, Night.AddHours(1)));
    }

    [Fact]
    public async Task Breakdown_TalliesMovesAndStats_FromTheGameChatOnly()
    {
        Roll(0, Aria, 6, 5, 1, "Hack and Slash", StatType.Str);
        Roll(1, Aria, 2, 3, 1, "Hack and Slash", StatType.Str);
        Roll(2, Bram, 4, 4, 0, "Defy Danger", StatType.Dex);
        Roll(3, Bram, 3, 3);
        Roll(4, Bram, 6, 6, 0, "Defy Danger", channel: 333);
        Roll(5, Dm, 6, 6, 0, "Hack and Slash");
        await _context.SaveChangesAsync();

        var breakdown = await Reader().GetBreakdownAsync(leaveOut: Dm);

        Assert.Equal(["Hack and Slash", "Defy Danger"], breakdown.Moves.Select(m => m.Name));
        Assert.Equal(new OutcomeTally("Hack and Slash", 2, 1, 0, 1), breakdown.Moves[0]);
        Assert.Equal(new OutcomeTally("Defy Danger", 1, 0, 1, 0), breakdown.Moves[1]);
        Assert.Equal(["+STR", "+DEX"], breakdown.Stats.Select(s => s.Name));
        Assert.Equal(1, breakdown.Unlabeled);
    }

    [Fact]
    public async Task Spreads_LeaveTheDmOutOfTheTable_ButStillShowTheirOwn()
    {
        Roll(0, Aria, 2, 2);
        Roll(1, Dm, 6, 6);
        await _context.SaveChangesAsync();

        var spreads = await Reader().GetSpreadsAsync(leaveOut: Dm);

        Assert.Equal(1, spreads.Table.Rolls);
        Assert.Equal(4, spreads.Table.AverageNatural);
        Assert.Equal(12, spreads.Players[Dm].AverageNatural);
    }

    [Fact]
    public async Task Sessions_AreMarkedOnes_OrSplitWhereNobodyRolledForAWhile()
    {
        var gap = RollReader.SessionGap.TotalMinutes;
        _context.Sessions.Add(new GameSession { Title = "The ford", StartedAt = Night.AddMinutes(-5), EndedAt = Night.AddMinutes(30) });
        Roll(0, Aria, 6, 6);
        Roll(10, Bram, 1, 1);
        Roll(gap + 60, Aria, 4, 4);
        Roll(gap + 61, Aria, 5, 4);
        Roll(3 * gap, Bram, 3, 3);
        await _context.SaveChangesAsync();

        var sessions = (await Reader().GetBreakdownAsync()).Sessions;

        // Newest first
        Assert.Equal([1, 2, 2], sessions.Select(s => s.Rolls));
        Assert.Equal("Session 1: The ford", sessions[2].Name);
        Assert.Equal(1, sessions[2].Number);
        Assert.Equal(2, sessions[2].Players);
        Assert.Equal(1, sessions[2].Natural12s);
        Assert.Equal(1, sessions[2].Natural2s);
        Assert.Null(sessions[1].Name);
    }

    [Fact]
    public async Task Streaks_AreTheRunEachPlayerIsOnNow()
    {
        Roll(0, Aria, 1, 1);
        Roll(1, Aria, 5, 5);
        Roll(2, Aria, 4, 4);
        Roll(3, Aria, 6, 5);
        Roll(4, Bram, 5, 5);
        Roll(5, Bram, 1, 2);
        await _context.SaveChangesAsync();

        var streaks = await Reader().GetStreaksAsync();

        Assert.Equal(new RollStreak(Hot: true, Length: 3), streaks[Aria]);
        Assert.False(streaks.ContainsKey(Bram));
    }

    [Fact]
    public async Task Awards_GoToWhoStoodOut_AndTheDmCantWin()
    {
        foreach (var minute in new[] { 0, 1, 2 })
            Roll(minute, Aria, 6, 5);
        Roll(3, Aria, 6, 6);
        foreach (var minute in new[] { 4, 5, 6 })
            Roll(minute, Bram, 1, 1);
        Roll(7, Dm, 6, 6);
        Roll(8, Dm, 6, 6);
        await _context.SaveChangesAsync();

        var awards = (await Reader().GetAwardsAsync(Night, Night.AddHours(1), leaveOut: Dm))!.Awards
            .ToDictionary(a => a.Title, a => a.UserID);

        Assert.Equal(Aria, awards["Hot dice"]);
        Assert.Equal(Bram, awards["Cursed dice"]);
        Assert.Equal(Aria, awards["Sharpshooter"]);
        Assert.Equal(Aria, awards["Busiest"]);
        Assert.Equal(Aria, awards["Blessed"]);
        Assert.Equal(Bram, awards["Snake charmer"]);
        Assert.DoesNotContain(Dm, awards.Values);
    }

    [Fact]
    public async Task Awards_AreNone_ForATimeNobodyRolled()
    {
        Roll(0, Aria, 6, 6);
        await _context.SaveChangesAsync();

        Assert.Null(await Reader().GetAwardsAsync(Night.AddDays(1), Night.AddDays(2)));
    }
}
