using DungeonWorldBot.Features.Rolls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// The bot's settings, bound from the top level of appsettings.json (or environment variables of the same names).
/// </summary>
public class BotSettings
{
    /// <summary>
    /// The Discord bot token, set with REMORA_BOT_TOKEN. The bot won't start without it.
    /// </summary>
    [ConfigurationKeyName("REMORA_BOT_TOKEN")]
    public string? BotToken { get; set; }

    /// <summary>
    /// The channel whose rolls count toward roll stats, set with GameChatChannelId.
    /// </summary>
    public ulong GameChatChannelID { get; set; } = RollStatsService.DefaultGameChatChannelID;

    /// <summary>
    /// The address people open the dashboard at (like https://pi.tailnet.ts.net), set with DashboardUrl.
    /// </summary>
    public string? DashboardUrl { get; set; }

    /// <summary>
    /// Where the DM's settings are kept, set with GameSettingsPath.
    /// </summary>
    public string GameSettingsPath { get; set; } = "game-settings.json";

    /// <summary>
    /// The server role that makes someone the DM, set with DungeonMasterRoleName.
    /// </summary>
    public string DungeonMasterRoleName { get; set; } = "DM";
}

public static class BotSettingsExtensions
{
    /// <summary>
    /// Binds <see cref="BotSettings"/> from the top level of the configuration.
    /// </summary>
    public static OptionsBuilder<BotSettings> AddBotSettings(this IServiceCollection services) =>
        services.AddOptions<BotSettings>().BindConfiguration("");
}
