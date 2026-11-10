namespace DungeonWorldBot.Dashboard.Models.Live;

/// <summary>
/// A database watched for changes, so open pages can refresh themselves.
/// </summary>
/// <param name="Name">What open pages are told changed: <see cref="Game"/> or <see cref="Scenes"/>.</param>
public record WatchedDatabase(string Name, string Path)
{
    /// <summary>
    /// The bot's database: characters, rolls, fights, sessions.
    /// </summary>
    public const string Game = "game";

    /// <summary>
    /// The dashboard's own, which changes with every stroke on a board. Few pages show anything from it.
    /// </summary>
    public const string Scenes = "scenes";
}
