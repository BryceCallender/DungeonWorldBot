namespace DungeonWorldBot.Features.GameMaster;

/// <summary>
/// Campaign-wide settings the DM controls from Discord, shared by every character.
/// </summary>
public interface IGameSettingsService
{
    /// <summary>
    /// Stat points gained for reaching a level.
    /// </summary>
    int StatPointsForLevel(int level);

    /// <summary>
    /// Points for levels without their own amount, and the levels that have one.
    /// </summary>
    (int Default, IReadOnlyDictionary<int, int> PerLevel) GetStatPointTable();

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
