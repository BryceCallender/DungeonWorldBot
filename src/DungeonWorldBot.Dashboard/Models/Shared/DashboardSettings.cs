namespace DungeonWorldBot.Dashboard.Models.Shared;

/// <summary>
/// How this campaign plays, for the things the dashboard can't tell from the database.
/// </summary>
/// <param name="DungeonMasterID">Whose rolls are the DM's, set with DungeonMasterId. Rolls only record who made them, not their roles. Who can do
/// DM things on the dashboard goes by role instead (see <see cref="Auth.DiscordSignIn"/>).</param>
/// <param name="TrackWeight">Whether the table counts item weight and load, set with TrackWeight. Off hides both.</param>
public record DashboardSettings(ulong DungeonMasterID, bool TrackWeight)
{
    public const ulong DefaultDungeonMasterID = 473305025301577728;

    public static DashboardSettings From(IConfiguration configuration) => new(
        configuration.GetValue<ulong?>("DungeonMasterId") ?? DefaultDungeonMasterID,
        configuration.GetValue("TrackWeight", false));
}
