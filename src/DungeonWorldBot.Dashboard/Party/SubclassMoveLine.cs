namespace DungeonWorldBot.Dashboard.Party;

/// <param name="Unlocked">Whether the character has reached the move's level.</param>
/// <param name="Hidden">Kept secret until the GM reveals it.</param>
public record SubclassMoveLine(int Level, string Name, string Summary, bool Unlocked, bool Hidden);
