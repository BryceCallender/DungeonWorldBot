namespace DungeonWorldBot.Dashboard.Models.Shared;

/// <summary>
/// How this campaign plays, for the things the dashboard can't tell from the database.
/// </summary>
/// <param name="DungeonMasterID">The DM's Discord ID, set with DungeonMasterId: whose rolls are the DM's, and the only
/// person who can do DM things on the dashboard once signed in, admin included.</param>
/// <param name="TrackWeight">Whether the table counts item weight and load, set with TrackWeight. Off hides both.</param>
/// <param name="GuildID">The Discord server, from Discord:GuildId, for links to messages; null leaves them out.</param>
public record DashboardSettings(ulong DungeonMasterID, bool TrackWeight, ulong? GuildID = null)
{
    public const ulong DefaultDungeonMasterID = 473305025301577728;

    public static DashboardSettings From(IConfiguration configuration) => new(
        configuration.GetValue<ulong?>("DungeonMasterId") ?? DefaultDungeonMasterID,
        configuration.GetValue("TrackWeight", false),
        configuration.GetValue<ulong?>("Discord:GuildId"));

    /// <summary>
    /// A link to a message in Discord, when the server is known.
    /// </summary>
    public string? MessageUrl(ulong channelID, ulong messageID) =>
        GuildID is { } guild ? $"https://discord.com/channels/{guild}/{channelID}/{messageID}" : null;
}
