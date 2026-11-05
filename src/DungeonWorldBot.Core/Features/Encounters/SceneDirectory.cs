using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The dashboard's encounter scenes, read straight from its own database, and their addresses.
/// </summary>
/// <remarks>
/// The dashboard keeps scenes in its own database, which the bot only reads (see <see cref="CanvasContext"/>). Set
/// DashboardUrl to the address people open the dashboard at (like https://pi.tailnet.ts.net).
/// Without DashboardUrl, links use the address of the last fight started from the dashboard, which knows its own.
/// </remarks>
[Scoped]
public class SceneDirectory(IOptions<BotSettings> settings, DungeonWorldContext context, CanvasContext canvases)
{
    private readonly string? _dashboardUrl = settings.Value.DashboardUrl?.TrimEnd('/');

    /// <summary>
    /// Encounter scenes that haven't been put away, newest first. None if the dashboard has never run here.
    /// </summary>
    public async Task<IReadOnlyList<SceneLink>> GetEncounterScenesAsync()
    {
        List<Canvas> scenes;
        try
        {
            scenes = await canvases.Canvases.AsNoTracking()
                .Where(c => c.Kind == CanvasKind.Encounter && c.ArchivedAt == null)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }
        catch (SqliteException)
        {
            // Not there because the dashboard has never run here, or mid-migration: no scenes to offer
            return [];
        }

        var origin = await DashboardOriginAsync();
        return scenes.Select(c =>
        {
            var id = CanvasIds.Encode(c.ID);
            return new SceneLink(id, c.Name, origin is null ? null : $"{origin}/scene/{id}");
        }).ToList();
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

        var lastUrl = await context.Encounters
            .Where(e => e.SceneUrl != null)
            .OrderByDescending(e => e.ID)
            .Select(e => e.SceneUrl)
            .FirstOrDefaultAsync();

        return Uri.TryCreate(lastUrl, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
}
