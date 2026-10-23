namespace DungeonWorldBot.Dashboard.Shared;

/// <summary>
/// How this campaign plays, for the things the dashboard can't tell from the database.
/// </summary>
/// <param name="DungeonMasterID">The DM, set with DungeonMasterId. The bot finds the DM by Discord role, which the dashboard can't see.</param>
/// <param name="TrackWeight">Whether the table counts item weight and load, set with TrackWeight. Off hides both.</param>
public record DashboardSettings(ulong DungeonMasterID, bool TrackWeight)
{
    public const ulong DefaultDungeonMasterID = 473305025301577728;

    public static DashboardSettings From(IConfiguration configuration) => new(
        configuration.GetValue<ulong?>("DungeonMasterId") ?? DefaultDungeonMasterID,
        configuration.GetValue("TrackWeight", false));
}
