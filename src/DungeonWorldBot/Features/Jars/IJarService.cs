using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Jars;

public interface IJarService
{
    /// <summary>
    /// Puts a corpse in an empty jar, giving the character their jars if they've lost them.
    /// </summary>
    /// <remarks>
    /// Check <see cref="Rules.JarRules.WhyCannotStore"/> first.
    /// </remarks>
    Task StoreAsync(Character character, string corpse);

    /// <summary>
    /// Takes a corpse out of its jar, leaving the jar empty.
    /// </summary>
    Task EmptyAsync(Character character, string corpse);
}
