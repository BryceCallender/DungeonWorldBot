using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Features.Rolls;
using Microsoft.EntityFrameworkCore;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Rolls;

public class RollQueriesTests
{
    private static readonly Snowflake Player = new(111);
    private static readonly Snowflake Channel = new(222);
    private static readonly DateTimeOffset Night = new(2026, 11, 1, 19, 0, 0, TimeSpan.Zero);

    // A message sent that many minutes into the night, with an ID like Discord's
    private static Snowflake At(int minutes) =>
        Snowflake.CreateTimestampSnowflake(Night.AddMinutes(minutes), Constants.DiscordEpoch);

    private static async Task RecordAsync(RollStatsService service, int minutes, int die) =>
        await service.RecordAsync(At(minutes), Channel, Player, new DiceRoller(new ConstantDieRoller(die)).Roll("2d6"));

    [Fact]
    public async Task Since_ReadsOnlyLaterRolls()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new RollStatsService(context, Options.Create(new BotSettings { GameChatChannelID = 0 }));
        foreach (var minutes in new[] { -60, -1, 0, 30 })
            await RecordAsync(service, minutes, 3);

        var rolls = await context.Rolls.Since(Night).ToListAsync();

        Assert.Equal([At(0), At(30)], rolls.Select(r => r.MessageID).Order());
    }

    [Fact]
    public async Task Until_KeepsRollsUpToTheEnd_AndBetweenSortsThemOldestFirst()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new RollStatsService(context, Options.Create(new BotSettings { GameChatChannelID = 0 }));
        foreach (var minutes in new[] { 90, 0, 60, 61 })
            await RecordAsync(service, minutes, 3);

        var rolls = await context.Rolls.Since(Night).Until(Night.AddMinutes(60)).OldestFirst().ToListAsync();

        Assert.Equal([At(0), At(60)], rolls.Select(r => r.MessageID));
    }

    [Fact]
    public async Task MissStreak_CountsBackToTheLastHit()
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var service = new RollStatsService(context, Options.Create(new BotSettings { GameChatChannelID = 0 }));
        // Recorded out of order, as a scan of older history would
        await RecordAsync(service, 3, 1);
        await RecordAsync(service, 0, 1);
        await RecordAsync(service, 1, 6);
        await RecordAsync(service, 2, 2);

        Assert.Equal(2, await service.MissStreakAsync(Player));
    }
}
