namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <param name="Name">A saved monster's name.</param>
/// <param name="Count">How many of it.</param>
public record MonsterCount(string Name, int Count);
