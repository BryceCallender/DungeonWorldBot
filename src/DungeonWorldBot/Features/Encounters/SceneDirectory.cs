using DungeonWorldBot.Data;
using DungeonWorldBot.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The dashboard's encounter scenes, read straight from its own database, and their addresses.
/// </summary>
/// <remarks>
/// The dashboard keeps scenes in canvases.db beside itself, which the bot only reads. Set CanvasDatabasePath if it's
/// somewhere else, and DashboardUrl to the address people open the dashboard at (like https://pi.tailnet.ts.net).
/// Without DashboardUrl, links use the address of the last fight started from the dashboard, which knows its own.
/// </remarks>
[Scoped]
public class SceneDirectory
{
    private readonly string _canvasDatabasePath;
    private readonly string? _dashboardUrl;
    private readonly DungeonWorldContext _context;

    public SceneDirectory(IConfiguration configuration, IHostEnvironment environment, DungeonWorldContext context)
    {
        _canvasDatabasePath = Path.GetFullPath(
            configuration.GetValue<string?>("CanvasDatabasePath")
            ?? Path.Combine(environment.ContentRootPath, "..", "DungeonWorldBot.Dashboard", "canvases.db"));
        _dashboardUrl = configuration.GetValue<string?>("DashboardUrl")?.TrimEnd('/');
        _context = context;
    }

    /// <summary>
    /// Encounter scenes that haven't been put away, newest first. None if the dashboard has never run here.
    /// </summary>
    public async Task<IReadOnlyList<SceneLink>> GetEncounterScenesAsync()
    {
        if (!File.Exists(_canvasDatabasePath))
            return [];

        var scenes = new List<(string ID, string Name)>();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_canvasDatabasePath};Mode=ReadOnly");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT ID, Name FROM Canvases WHERE Kind = 'Encounter' AND ArchivedAt IS NULL ORDER BY UpdatedAt DESC";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                scenes.Add((CanvasIds.Encode(reader.GetInt32(0)), reader.GetString(1)));
        }
        catch (SqliteException)
        {
            // Mid-migration or not a canvas database: no scenes to offer
            return [];
        }

        var origin = await DashboardOriginAsync();
        return scenes.Select(s => new SceneLink(s.ID, s.Name, origin is null ? null : $"{origin}/scene/{s.ID}")).ToList();
    }

    /// <summary>
    /// An encounter scene by its short ID or its name, whatever its capitalization.
    /// </summary>
    public async Task<SceneLink?> FindAsync(string idOrName)
    {
        var wanted = idOrName.Trim();
        var scenes = await GetEncounterScenesAsync();
        return scenes.FirstOrDefault(s => s.ID == wanted)
               ?? scenes.FirstOrDefault(s => s.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> DashboardOriginAsync()
    {
        if (_dashboardUrl is not null)
            return _dashboardUrl;

        var lastUrl = await _context.Encounters
            .Where(e => e.SceneUrl != null)
            .OrderByDescending(e => e.ID)
            .Select(e => e.SceneUrl)
            .FirstOrDefaultAsync();

        return Uri.TryCreate(lastUrl, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
}
