using System.Net;
using System.Text;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.API.Objects;
using Remora.Discord.Caching.Responders;
using Remora.Discord.Rest;
using Remora.Discord.Rest.Extensions;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Infrastructure;

/// <summary>
/// The server's role list, which the DM check reads on every DM command, against a fake Discord that counts requests.
/// </summary>
public class RoleCacheTests
{
    private static readonly Snowflake Guild = new(1);

    private const string Roles = """[{"id":"10","name":"DM","color":0,"hoist":false,"position":1,"permissions":"0","managed":false,"mentionable":false,"flags":0}]""";

    private sealed class FakeDiscord : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Roles, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private static (IServiceProvider Services, FakeDiscord Discord, FakeClock Clock) Build()
    {
        var discord = new FakeDiscord();
        var clock = new FakeClock();
        var services = new ServiceCollection()
            .AddDiscordRest(_ => ("token", DiscordTokenType.Bot), client => client.ConfigurePrimaryHttpMessageHandler(() => discord))
            .AddRemoraServices();
#pragma warning disable CS0618 // MemoryCache still reads its clock from here
        services.Configure<MemoryCacheOptions>(o => o.Clock = clock);
#pragma warning restore CS0618
        return (services.BuildServiceProvider(), discord, clock);
    }

    [Fact]
    public async Task RoleList_IsKeptBetweenCommands_ThenAskedForAgain()
    {
        var (services, discord, clock) = Build();
        var guilds = services.GetRequiredService<IDiscordRestGuildAPI>();

        Assert.True((await guilds.GetGuildRolesAsync(Guild)).IsSuccess);
        clock.UtcNow += TimeSpan.FromMinutes(2);
        await guilds.GetGuildRolesAsync(Guild);
        Assert.Equal(1, discord.Requests);

        clock.UtcNow += ServiceExtensions.RoleListLifetime;
        await guilds.GetGuildRolesAsync(Guild);
        Assert.Equal(2, discord.Requests);
    }

    [Fact]
    public async Task RenamedRoles_AreSeenWithoutAskingDiscord()
    {
        var (services, discord, _) = Build();
        var guilds = services.GetRequiredService<IDiscordRestGuildAPI>();
        var role = (Role)(await guilds.GetGuildRolesAsync(Guild)).Entity[0];

        await services.GetRequiredService<EarlyCacheResponder>().RespondAsync(new GuildRoleUpdate(Guild, role with { Name = "GM" }));

        var roles = await guilds.GetGuildRolesAsync(Guild);
        Assert.Equal("GM", Assert.Single(roles.Entity).Name);
        Assert.Equal(1, discord.Requests);
    }
}
