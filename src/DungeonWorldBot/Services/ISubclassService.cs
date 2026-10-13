using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Subclasses;

namespace DungeonWorldBot.Services;

public interface ISubclassService
{
    /// <summary>
    /// Makes the character a member of the subclass and gives them every move it has at their level.
    /// </summary>
    /// <returns>The moves learned.</returns>
    Task<IReadOnlyList<ClassMove>> JoinAsync(Character character, Subclass subclass);

    /// <summary>
    /// Removes the subclass, its moves, and anything it was tracking.
    /// </summary>
    Task LeaveAsync(Character character, Subclass subclass);

    /// <summary>
    /// Gives the character any of their subclass's moves they've reached the level for but don't have yet.
    /// </summary>
    /// <returns>The moves learned.</returns>
    Task<IReadOnlyList<ClassMove>> GrantUnlockedMovesAsync(Character character);
}
