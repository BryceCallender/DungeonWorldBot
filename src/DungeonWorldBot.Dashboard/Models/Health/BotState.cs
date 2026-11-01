namespace DungeonWorldBot.Dashboard.Models.Health;

/// <summary>
/// Whether the bot is up, from how recently it wrote its status.
/// </summary>
public enum BotState
{
    /// <summary>It's never written a status beside this database.</summary>
    Unknown,

    Online,

    /// <summary>It shut down cleanly.</summary>
    Stopped,

    /// <summary>It stopped writing without saying it was shutting down: crashed, or the Pi lost power.</summary>
    Down,
}
