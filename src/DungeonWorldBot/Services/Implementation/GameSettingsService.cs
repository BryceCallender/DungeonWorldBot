using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DungeonWorldBot.Services.Implementation;

/// <summary>
/// Keeps the DM's settings in a small JSON file (game-settings.json by default) rather than the database.
/// </summary>
public class GameSettingsService : IGameSettingsService
{
    private const string DefaultPath = "game-settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Settings are shared by every command, so reads and writes take turns
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly string _path;

    public GameSettingsService(IConfiguration? configuration = null, string? path = null)
    {
        _path = path ?? configuration?.GetValue<string?>("GameSettingsPath") ?? DefaultPath;
    }

    private sealed class Settings
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
        FileLock.Wait();
        try
        {
            return ReadFile();
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
            var settings = ReadFile();
            change(settings);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        finally
        {
            FileLock.Release();
        }
    }

    private Settings ReadFile() =>
        File.Exists(_path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new Settings() : new Settings();
}
