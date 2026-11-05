using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Features.Rolls;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Rolls;

public class ChangeOutcomeTests
{
    private static readonly Snowflake Roll = new(1);
    private static readonly Snowflake Roller = new(111);
    private static readonly Snowflake SomeoneElse = new(222);

    private static async Task<bool> MayChangeAsync(bool recorded, Snowflake self, bool isGameMaster)
    {
        using var database = new TestDatabase();
        await using var context = database.CreateContext();
        var rollStats = new RollStatsService(context, Options.Create(new BotSettings { GameChatChannelID = 0 }));
        if (recorded)
            await rollStats.RecordAsync(Roll, new Snowflake(333), Roller, new DiceRoller(new ConstantDieRoller(3)).Roll("2d6"));

        return await ChangeOutcomeCommand.MayChangeAsync(rollStats, new FakeGameMaster(isGameMaster, self), Roll, CancellationToken.None);
    }

    [Fact]
    public async Task Rollers_CanChangeTheirOwnRolls() =>
        Assert.True(await MayChangeAsync(recorded: true, Roller, isGameMaster: false));

    [Fact]
    public async Task Players_CantChangeSomeoneElsesRoll() =>
        Assert.False(await MayChangeAsync(recorded: true, SomeoneElse, isGameMaster: false));

    [Fact]
    public async Task TheDm_CanChangeAnyRoll() =>
        Assert.True(await MayChangeAsync(recorded: true, SomeoneElse, isGameMaster: true));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RollsNobodyMade_OnlyTheDmCanChange(bool isGameMaster, bool allowed) =>
        Assert.Equal(allowed, await MayChangeAsync(recorded: false, Roller, isGameMaster));
}
