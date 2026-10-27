namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// A move asked for from a board, waiting for the bot to roll it.
/// </summary>
public record WaitingMove(int CanvasID, string PersonID, string Actor, string Target, DateTimeOffset AskedAt);
