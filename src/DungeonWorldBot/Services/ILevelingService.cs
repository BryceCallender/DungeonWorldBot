using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Services;

public interface ILevelingService
{
    /// <summary>
    /// Changes the character's level. Each level gained gives the DM's chosen stat points (1 by default), and level-based resources (like a Ranger's
    /// Cleric Level) move with it.
    /// </summary>
    Task ChangeLevelAsync(Character character, int level);

    int GetStatPoints(Character character);

    Task GrantStatPointsAsync(Character character, int amount);

    /// <summary>
    /// Spends a stat point to raise a stat by 1.
    /// </summary>
    /// <returns>Why it couldn't be done, or null if it worked.</returns>
    Task<string?> AllocateStatAsync(Character character, StatType statType);
}
