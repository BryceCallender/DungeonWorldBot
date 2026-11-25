using DungeonWorldBot.Features.Bridge;

namespace DungeonWorldBot.Dashboard.Models.Bridge;

/// <summary>
/// A bot command someone wants to run (or have values suggested for) from the dashboard.
/// </summary>
/// <param name="Command">Its path, e.g. "shapeshift into".</param>
/// <param name="As">Whose Discord ID it runs as: their character's, or the DM's.</param>
/// <param name="Options">Each option filled in, by its name, as text.</param>
/// <param name="Focused">For suggestions, the option being filled in.</param>
public record CommandAsk(string Command, ulong As, IReadOnlyDictionary<string, string> Options, string? Focused = null);

/// <param name="Error">Why it wasn't asked for, or null when it was.</param>
/// <param name="ID">The request the bot will answer.</param>
public record CommandAsked(string? Error, int ID = 0)
{
    public static CommandAsked Failed(string error) => new(error);
}

/// <summary>
/// How a command asked for from the dashboard went.
/// </summary>
/// <param name="Done">Whether the bot has answered, or given up. False while it's waiting.</param>
/// <param name="Html">What the bot posted, shown as on Discord.</param>
public record CommandStatus(bool Done, string? Error = null, string? Html = null, IReadOnlyList<CatalogChoice>? Suggestions = null)
{
    public static readonly CommandStatus Waiting = new(false);
}

/// <summary>
/// Someone a command can be pointed at, like who to give coins to.
/// </summary>
public record MenuPerson(string ID, string Name);

/// <summary>
/// What the dashboard offers to run: the bot's commands the viewer may use, and the party to pick from.
/// </summary>
/// <param name="Playing">The ID of the character the viewer is playing, which commands act on without being told.</param>
/// <param name="GameMaster">Whether the viewer runs the game, and so may point commands at anyone's character.</param>
public record CommandMenu(IReadOnlyList<CatalogCommand> Commands, IReadOnlyList<MenuPerson> Party, string? Playing = null, bool GameMaster = false);
