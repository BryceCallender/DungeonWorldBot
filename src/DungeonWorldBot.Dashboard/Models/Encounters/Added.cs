using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <param name="Error">Why it couldn't, or null once it has.</param>
/// <param name="Enemies">The enemies added.</param>
public record Added(string? Error, IReadOnlyList<Enemy> Enemies)
{
    public static Added Failed(string error) => new(error, []);
}
