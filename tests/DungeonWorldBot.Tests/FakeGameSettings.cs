using DungeonWorldBot.Features.GameMaster;

namespace DungeonWorldBot.Tests;

/// <summary>
/// The DM's settings kept in memory rather than a file: one stat point a level, as Dungeon World has it.
/// </summary>
public class FakeGameSettings : IGameSettingsService
{
    private readonly Dictionary<int, int> _perLevel = [];
    private int _default = 1;

    public int StatPointsForLevel(int level) => _perLevel.GetValueOrDefault(level, _default);

    public StatPointTable GetStatPointTable() => new(_default, _perLevel);

    public Task SetDefaultStatPointsAsync(int points)
    {
        _default = points;
        _perLevel.Clear();
        return Task.CompletedTask;
    }

    public Task SetStatPointsForLevelAsync(int level, int points)
    {
        _perLevel[level] = points;
        return Task.CompletedTask;
    }

    public bool DramaticRolls { get; private set; } = true;

    public Task SetDramaticRollsAsync(bool on)
    {
        DramaticRolls = on;
        return Task.CompletedTask;
    }
}
