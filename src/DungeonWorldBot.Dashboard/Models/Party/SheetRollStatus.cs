namespace DungeonWorldBot.Dashboard.Models.Party;

/// <summary>
/// How a move rolled from a sheet went, for the sheet to show once the bot has rolled it.
/// </summary>
/// <param name="Rolled">Whether the bot has rolled it, or given up on it. False while it's waiting.</param>
/// <param name="Summary">A few words on how it went, e.g. "Weak hit".</param>
/// <param name="Error">Why the bot couldn't make it, if it couldn't.</param>
public record SheetRollStatus(bool Rolled, string? Summary = null, string? Error = null)
{
    public static readonly SheetRollStatus Waiting = new(false);
}
