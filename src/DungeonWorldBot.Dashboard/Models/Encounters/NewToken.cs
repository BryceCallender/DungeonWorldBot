namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// A token to put on the board.
/// </summary>
/// <param name="Picture">The address of a picture to fill its circle with instead of initials.</param>
/// <param name="Fighter">Who it is in the fight, for making moves from it.</param>
public record NewToken(string Name, string? Picture = null, TokenFighter? Fighter = null);
