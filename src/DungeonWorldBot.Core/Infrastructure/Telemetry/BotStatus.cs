using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// How the bot is doing, written beside its database every little while (see <see cref="BotStatusWriter"/>) for the
/// dashboard's health page, which can't ask the bot directly.
/// </summary>
/// <param name="LastBeat">When it last wrote this. Much older than <see cref="BotStatusWriter.Interval"/> means it's down.</param>
/// <param name="StoppedAt">When it shut down cleanly, or null while it's running (or if it crashed).</param>
/// <param name="Commands">Each command run since it started, most run first.</param>
/// <param name="RecentErrors">Its last few errors, newest first.</param>
public record BotStatus(
    DateTimeOffset StartedAt,
    DateTimeOffset LastBeat,
    DateTimeOffset? StoppedAt,
    string Version,
    string Machine,
    long MemoryBytes,
    IReadOnlyList<CommandCount> Commands,
    IReadOnlyList<LoggedError> RecentErrors)
{
    /// <summary>
    /// Where the status of the bot using the database at <paramref name="databasePath"/> is kept, e.g.
    /// SanaSolem.status.json beside SanaSolem.db.
    /// </summary>
    public static string PathFor(string databasePath) => Path.ChangeExtension(Path.GetFullPath(databasePath), ".status.json");

    /// <returns>The status, or null when the bot hasn't written one (or it can't be read just now).</returns>
    public static async Task<BotStatus?> ReadAsync(string databasePath, CancellationToken ct = default)
    {
        try
        {
            await using var file = File.OpenRead(PathFor(databasePath));
            return await JsonSerializer.DeserializeAsync(file, BotStatusJson.Default.BotStatus, ct);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// The status file's JSON, worked out when the bot is built rather than by reflection, which trimmed builds turn off.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(BotStatus))]
public partial class BotStatusJson : JsonSerializerContext;
