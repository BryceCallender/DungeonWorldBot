using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Services;

public interface IResourceService
{
    /// <summary>
    /// The resource's current value, or its starting value if the character hasn't used it yet.
    /// </summary>
    int GetValue(Character character, ClassResource resource);

    /// <summary>
    /// The resource's max for this character, which some moves raise, or null if it has none.
    /// </summary>
    int? GetMax(Character character, ClassResource resource);

    /// <summary>
    /// The value it goes back to when reset.
    /// </summary>
    int GetResetValue(Character character, ClassResource resource);

    /// <summary>
    /// The value for display, e.g. "2/3".
    /// </summary>
    string Format(Character character, ClassResource resource);

    /// <returns>The value actually stored, after keeping it between 0 and the resource's max.</returns>
    Task<int> SetValueAsync(Character character, ClassResource resource, int value);
}
