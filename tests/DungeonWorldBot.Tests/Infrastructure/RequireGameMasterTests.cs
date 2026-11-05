using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Remora.Commands.Attributes;
using Remora.Commands.Extensions;
using Remora.Commands.Groups;
using Remora.Commands.Services;
using Remora.Results;

namespace DungeonWorldBot.Tests.Infrastructure;

public class RequireGameMasterTests
{
    [Group("outer")]
    public class OuterGroup : CommandGroup
    {
        // Like the groups under /dm: the condition is on the nested group, not the top-level one
        [Group("inner")]
        [RequireGameMaster]
        public class InnerGroup : CommandGroup
        {
            [Command("run")]
            public Task<IResult> RunAsync() => Task.FromResult<IResult>(Result.Success);
        }

        [Group("hinted")]
        public class HintedGroup : CommandGroup
        {
            [Command("run")]
            [RequireGameMaster("Ask nicely.")]
            public Task<IResult> RunAsync() => Task.FromResult<IResult>(Result.Success);
        }
    }

    private static async Task<IResult> RunAsync(string command, bool isGameMaster)
    {
        var services = new ServiceCollection()
            .AddCommands()
            .AddCommandTree().WithCommandGroup<OuterGroup>().Finish()
            .AddCondition<RequireGameMasterCondition>()
            .AddSingleton<IGameMasterService>(new FakeGameMaster(isGameMaster))
            .BuildServiceProvider();

        return await services.GetRequiredService<CommandService>().TryExecuteAsync(command, services);
    }

    [Theory]
    [InlineData("outer inner run")]
    [InlineData("outer hinted run")]
    public async Task GameMaster_CanRun(string command)
    {
        Assert.True((await RunAsync(command, isGameMaster: true)).IsSuccess);
    }

    [Fact]
    public async Task Players_AreRefused_WithTheReason()
    {
        var result = await RunAsync("outer inner run", isGameMaster: false);

        Assert.False(result.IsSuccess);
        var refused = CommandErrorReplies.Find<NotGameMasterError>(result);
        Assert.Equal("Only someone with the DM role can do that.", refused?.Message);
    }

    [Fact]
    public async Task Refusals_IncludeTheHint()
    {
        var result = await RunAsync("outer hinted run", isGameMaster: false);

        Assert.Equal("Only someone with the DM role can do that. Ask nicely.", CommandErrorReplies.Find<NotGameMasterError>(result)?.Message);
    }
}
