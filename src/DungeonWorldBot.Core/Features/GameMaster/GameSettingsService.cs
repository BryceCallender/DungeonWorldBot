using DungeonWorldBot.Infrastructure;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DungeonWorldBot.Features.GameMaster;

/// <summary>
/// Keeps the DM's settings in a small JSON file (game-settings.json by default) rather than the database.
/// </summary>
/// <remarks>
/// The file is replaced whole on every save, so reading it needs no lock: a read sees the settings from before a save
/// or after it, never half of each. Only saves take turns.
/// </remarks>
[Singleton<IGameSettingsService>]
public class GameSettingsService(IOptions<BotSettings> options) : IGameSettingsService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path = options.Value.GameSettingsPath;

    // Two DM commands saving at once would otherwise each save over the other's change
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private volatile Snapshot? _snapshot;

    private class Settings
    {
        // Each level gives 1 stat point unless the DM says otherwise (the Dungeon World rule)
        public int StatPointsPerLevel { get; set; } = 1;
        public Dictionary<int, int> StatPointsAtLevel { get; set; } = new();
        public bool DramaticRolls { get; set; } = true;
    }

    private record Snapshot(Settings Settings, DateTime WrittenAt);

    public int StatPointsForLevel(int level)
    {
        var settings = Current();
        return settings.StatPointsAtLevel.GetValueOrDefault(level, settings.StatPointsPerLevel);
    }

    public StatPointTable GetStatPointTable()
    {
        var settings = Current();
        return new StatPointTable(settings.StatPointsPerLevel, settings.StatPointsAtLevel);
    }

    public Task SetDefaultStatPointsAsync(int points) => UpdateAsync(s =>
    {
        s.StatPointsPerLevel = points;
        s.StatPointsAtLevel.Clear();
    });

    public Task SetStatPointsForLevelAsync(int level, int points) => UpdateAsync(s =>
    {
        // An amount matching the default is just the default
        if (points == s.StatPointsPerLevel)
            s.StatPointsAtLevel.Remove(level);
        else
            s.StatPointsAtLevel[level] = points;
    });

    public bool DramaticRolls => Current().DramaticRolls;

    public Task SetDramaticRollsAsync(bool on) => UpdateAsync(s => s.DramaticRolls = on);

    /// <summary>
    /// The settings as the file has them. It's only read again when it changes, so hand edits still take effect
    /// without parsing it on every call.
    /// </summary>
    private Settings Current()
    {
        var writtenAt = LastWriteTime();
        if (_snapshot is { } snapshot && snapshot.WrittenAt == writtenAt)
            return snapshot.Settings;

        try
        {
            var settings = Read();
            _snapshot = new Snapshot(settings, writtenAt);
            return settings;
        }
        catch (JsonException) when (_snapshot is { } last)
        {
            // Caught mid hand-edit: the last settings that made sense stand until it's saved whole
            return last.Settings;
        }
    }

    private async Task UpdateAsync(Action<Settings> change)
    {
        await _saveLock.WaitAsync();
        try
        {
            // Changed on a fresh copy, so tables already handed out never change underneath their callers
            var settings = Read();
            change(settings);
            await WriteAsync(settings);
            _snapshot = new Snapshot(settings, LastWriteTime());
        }
        finally
        {
            _saveLock.Release();
        }
    }

    // Written beside the file, then moved over it, so a crash partway leaves the old settings rather than half a file
    private async Task WriteAsync(Settings settings)
    {
        var saving = _path + ".saving";
        await File.WriteAllTextAsync(saving, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(saving, _path, overwrite: true);
    }

    private Settings Read() =>
        File.Exists(_path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings() : new Settings();

    public void Dispose()
    {
        _saveLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private DateTime LastWriteTime() => File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue;
}
