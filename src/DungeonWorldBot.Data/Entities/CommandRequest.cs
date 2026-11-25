using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public enum CommandRequestKind
{
    /// <summary>
    /// Run the command, as if the player had typed it in game chat.
    /// </summary>
    Run,

    /// <summary>
    /// Suggest values for one of the command's options, as Discord's autocomplete does while typing.
    /// </summary>
    Suggest,
}

/// <summary>
/// A slash command someone ran from the dashboard. The dashboard can't run the bot's commands or reach Discord, so it
/// only asks, and the bot runs the command a moment later as that player, by the same rules as in Discord. Its replies
/// go to game chat as usual and are copied into <see cref="Reply"/> for the dashboard to show.
/// </summary>
public class CommandRequest
{
    public int ID { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>
    /// Who asked, by Discord ID. Null when the dashboard's sign-in is off.
    /// </summary>
    public Snowflake? RequestedBy { get; set; }

    /// <summary>
    /// Who the command runs as: the player whose character it acts on, as if they'd typed it.
    /// </summary>
    public Snowflake UserID { get; set; }

    public CommandRequestKind Kind { get; set; }

    /// <summary>
    /// The command's path, as in Discord without the slash, e.g. "shapeshift into".
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// The options given, as a JSON object of each option's name to its value as text, e.g. {"form":"Wolf","bonus":"1"}.
    /// </summary>
    public string Options { get; set; } = "{}";

    /// <summary>
    /// For <see cref="CommandRequestKind.Suggest"/>, the option to suggest values for.
    /// </summary>
    public string? Focused { get; set; }

    /// <summary>
    /// When the bot ran it, or gave up on it. Null while it's waiting.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Why the bot couldn't run it, if it couldn't.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// What the bot said, as JSON: the messages it posted for a run, or the values it suggested.
    /// </summary>
    public string? Reply { get; set; }
}
