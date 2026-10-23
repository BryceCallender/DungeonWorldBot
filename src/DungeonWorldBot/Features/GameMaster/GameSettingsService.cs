using DungeonWorldBot.Infrastructure;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DungeonWorldBot.Features.GameMaster;

/// <summary>
/// Keeps the DM's settings in a small JSON file (game-settings.json by default) rather than the database.
/// </summary>
[Singleton<IGameSettingsService>]
public class GameSettingsService : IGameSettingsService
{
    private const string DefaultPath = "game-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Settings are shared by every command, so reads and writes take turns
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly string _path;

    private volatile CachedSettings? _cached;

    public GameSettingsService(IConfiguration? configuration = null, string? path = null)
    {
        _path = path ?? configuration?.GetValue<string?>("GameSettingsPath") ?? DefaultPath;
    }

    private class Settings
    {
        // Each level gives 1 stat point unless the DM says otherwise (the Dungeon World rule)
        public int StatPointsPerLevel { get; set; } = 1;
        public Dictionary<int, int> StatPointsAtLevel { get; set; } = new();
    }

    public int StatPointsForLevel(int level)
    {
        var settings = Load();
        return settings.StatPointsAtLevel.TryGetValue(level, out var points) ? points : settings.StatPointsPerLevel;
    }

    public (int Default, IReadOnlyDictionary<int, int> PerLevel) GetStatPointTable()
    {
        var settings = Load();
        return (settings.StatPointsPerLevel, settings.StatPointsAtLevel);
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

    private Settings Load()
    {
        // The file is only read again when it changes, so hand edits still take effect without parsing it every call
        var cached = _cached;
        if (cached is not null && cached.WrittenAt == LastWriteTime())
            return cached.Settings;

        FileLock.Wait();
        try
        {
            return Refresh().Settings;
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task UpdateAsync(Action<Settings> change)
    {
        await FileLock.WaitAsync();
        try
        {
            // Changes go to a fresh copy so tables already handed out never change underneath their callers
            var settings = ReadFile();
            change(settings);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings, JsonOptions));
            _cached = new CachedSettings(settings, LastWriteTime());
        }
        finally
        {
            FileLock.Release();
        }
    }

    private CachedSettings Refresh()
    {
        var writtenAt = LastWriteTime();
        var settings = ReadFile();
        return _cached = new CachedSettings(settings, writtenAt);
    }

    private DateTime LastWriteTime() => File.Exists(_path) ? File.GetLastWriteTimeUtc(_path) : DateTime.MinValue;

    private Settings ReadFile() =>
        File.Exists(_path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings() : new Settings();

    private record CachedSettings(Settings Settings, DateTime WrittenAt);
}
