using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DungeonWorldBot.Tests.Infrastructure;

public class BotSettingsTests
{
    private static BotSettings Bind(Dictionary<string, string?> values) =>
        new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .AddBotSettings().Services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<BotSettings>>().Value;

    [Fact]
    public void Settings_ComeFromTheTopLevelKeys()
    {
        var settings = Bind(new()
        {
            ["REMORA_BOT_TOKEN"] = "token",
            ["GameChatChannelId"] = "222",
            ["DashboardUrl"] = "https://pi.example",
            ["GameSettingsPath"] = "elsewhere.json",
            ["DungeonMasterRoleName"] = "GM",
        });

        Assert.Equal("token", settings.BotToken);
        Assert.Equal(222UL, settings.GameChatChannelID);
        Assert.Equal("https://pi.example", settings.DashboardUrl);
        Assert.Equal("elsewhere.json", settings.GameSettingsPath);
        Assert.Equal("GM", settings.DungeonMasterRoleName);
    }

    [Fact]
    public void Settings_LeftOut_KeepTheirDefaults()
    {
        var settings = Bind([]);

        Assert.Null(settings.BotToken);
        Assert.Equal(RollStatsService.DefaultGameChatChannelID, settings.GameChatChannelID);
        Assert.Null(settings.DashboardUrl);
        Assert.Equal("game-settings.json", settings.GameSettingsPath);
        Assert.Equal("DM", settings.DungeonMasterRoleName);
    }
}
