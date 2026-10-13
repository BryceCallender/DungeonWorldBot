using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Moves;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Services;
using DungeonWorldBot.Services.Implementation;
using DungeonWorldBot.Stats;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests;

public class RollStatsTests
{
    [Theory]
    [InlineData("2d6", 6, 6, 12)]
    [InlineData("2d6+1", 1, 1, 3)]
    [InlineData("2d6-2", 4, 4, 6)]
    public void CurrentRollEmbeds_AreReadBack(string expression, int first, int second, int total)
    {
        var roll = new DiceRoller(new ConstantDieRoller(first)).Roll(expression);

        var dice = MoveDice.FromDescription(DiceHelper.BuildDiceResult(roll));

        Assert.Equal(new MoveDice(first, second, total), dice);
    }

    [Fact]
    public void MoveEmbeds_AreReadPastTheTrigger()
    {
        var roll = new DiceRoller(new ConstantDieRoller(5)).Roll("2d6+2");

        var dice = MoveDice.FromDescription($"*When you attack an enemy in melee...*\n\n{DiceHelper.BuildDiceResult(roll)}");

        Assert.Equal(new MoveDice(5, 5, 12), dice);
        Assert.Equal(MoveOutcome.StrongHit, dice!.Outcome);
    }

    [Theory]
    // 2025: EdCanHack.DiceNotation, which listed modifiers as "constant"
    [InlineData("d6 + d6\n\n1 + 1 = 2\n", 1, 1, 2)]
    [InlineData("d6 + d6 + constant\n\n5 + 4 + 1 = 10\n", 5, 4, 10)]
    [InlineData("d6 + d6 + constant\n\n6 + 6 + -1 = 11\n", 6, 6, 11)]
    // 2022: the bot's own roller
    [InlineData("2d6+1\n\nTotal Roll: 8\n\n(3+4)+(1)=8", 3, 4, 8)]
    [InlineData("2d6\n\nTotal Roll: 2\n\n(1+1)=2", 1, 1, 2)]
    public void OldRollEmbeds_AreReadBack(string description, int first, int second, int total)
    {
        Assert.Equal(new MoveDice(first, second, total), MoveDice.FromDescription(description));
    }

    [Theory]
    [InlineData("d20\n\n15\n")]
    [InlineData("d8 + constant\n\n4 + 2 = 6\n")]
    [InlineData("d6 + d6 + d6\n\n1 + 2 + 3 = 6\n")]
    [InlineData("```\nd6 + d6\n 6 +  6 = 12\n\ndropped: 1, 2\n```")]
    [InlineData("`(2d6+1)x2`\n3, 4 → **16**")]
    [InlineData("d10\n\nTotal = 7")]
    [InlineData("You need a character to roll +DEX. Try using /character create")]
    public void OtherRolls_AreNotMoveRolls(string description)
    {
        Assert.Null(MoveDice.FromDescription(description));
    }

    [Fact]
    public void DamageRolls_AreNotRecorded()
    {
        Assert.Null(MoveDice.From(new DiceRoller().Roll("d10+2")));
        Assert.Null(MoveDice.From(new DiceRoller().Roll("4d6k2")));
        Assert.NotNull(MoveDice.From(new DiceRoller().Roll("2d6+1")));
    }

    [Fact]
    public async Task Stats_CountOutcomesAndNaturals_WithoutDoubleCounting()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using var context = new DungeonWorldContext(
            new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(connection).Options);
        await context.Database.MigrateAsync();
        var service = new RollStatsService(context, GameChat(222));

        var player = new Snowflake(111);
        var channel = new Snowflake(222);
        var otherChannel = new Snowflake(333);

        // Recorded live...
        await service.RecordAsync(new Snowflake(1), channel, player, new DiceRoller(new ConstantDieRoller(6)).Roll("2d6"));
        await service.RecordAsync(new Snowflake(2), channel, player, new DiceRoller(new ConstantDieRoller(1)).Roll("2d6+1"));
        await service.RecordAsync(new Snowflake(3), channel, player, new DiceRoller(new ConstantDieRoller(4)).Roll("d10"));

        // ...then found again by a scan, along with an older roll
        var added = await service.AddMissingAsync(new[]
        {
            new RollRecord { MessageID = new Snowflake(1), ChannelID = channel, UserID = player, First = 6, Second = 6, Total = 12 },
            new RollRecord { MessageID = new Snowflake(4), ChannelID = channel, UserID = player, First = 3, Second = 4, Total = 8 },
        });

        Assert.Equal(1, added);

        // Rolls outside the game chat are kept but don't count
        await service.RecordAsync(new Snowflake(5), otherChannel, player, new DiceRoller(new ConstantDieRoller(6)).Roll("2d6"));

        var stats = Assert.Single(await service.GetStatsAsync());
        Assert.Equal(new PlayerRollStats(player, Rolls: 3, StrongHits: 1, WeakHits: 1, Misses: 1, Natural12s: 1, Natural2s: 1), stats);
    }

    [Fact]
    public async Task Stats_AreEmptyWithoutAGameChat()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        await using var context = new DungeonWorldContext(
            new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new RollStatsService(context);

        await service.RecordAsync(new Snowflake(1), new Snowflake(222), new Snowflake(111), new DiceRoller(new ConstantDieRoller(6)).Roll("2d6"));

        Assert.Null(service.GameChatChannelID);
        Assert.Empty(await service.GetStatsAsync());
    }

    [Fact]
    public void GameChat_DefaultsToTheCampaignChannel()
    {
        var empty = new ConfigurationBuilder().Build();

        Assert.Equal(new Snowflake(RollStatsService.DefaultGameChatChannelID), new RollStatsService(null!, empty).GameChatChannelID);
        Assert.Equal(new Snowflake(222), new RollStatsService(null!, GameChat(222)).GameChatChannelID);
    }

    private static IConfiguration GameChat(ulong channelID) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GameChatChannelId"] = channelID.ToString() }).Build();
}
