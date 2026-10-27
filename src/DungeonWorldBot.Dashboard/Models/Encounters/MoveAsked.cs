namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <param name="Error">Why it wasn't asked for, or null when it was.</param>
/// <param name="ID">The request the bot will roll.</param>
public record MoveAsked(string? Error, int ID = 0)
{
    public static MoveAsked Failed(string error) => new(error);
}
