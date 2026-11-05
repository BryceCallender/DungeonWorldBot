namespace DungeonWorldBot.Features.GameMaster;

/// <summary>
/// The stat points each level gives.
/// </summary>
/// <param name="Default">Points for levels without their own amount.</param>
/// <param name="PerLevel">The levels that have their own amount, and what it is.</param>
public record StatPointTable(int Default, IReadOnlyDictionary<int, int> PerLevel);

/// <summary>
/// Campaign-wide settings the DM controls from Discord, shared by every character.
/// </summary>
public interface IGameSettingsService
{
    /// <summary>
    /// Stat points gained for reaching a level.
    /// </summary>
    int StatPointsForLevel(int level);

    StatPointTable GetStatPointTable();

    /// <summary>
    /// Sets the points for every level, clearing any per-level amounts.
    /// </summary>
    Task SetDefaultStatPointsAsync(int points);

    Task SetStatPointsForLevelAsync(int level, int points);

    /// <summary>
    /// Whether big rolls (double sixes, snake eyes, Last Breath and the like) land a moment late with a headline. On
    /// unless the DM turns it off for a quicker game.
    /// </summary>
    bool DramaticRolls { get; }

    Task SetDramaticRollsAsync(bool on);
}
