using System.Globalization;
using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Sessions;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Dashboard.Services.Sessions;
using DungeonWorldBot.Dashboard.Services.Shared;
using Microsoft.AspNetCore.Mvc;

namespace DungeonWorldBot.Dashboard.Services.Scenes;

/// <summary>
/// The DM's changes to the campaign's map from the journey pages: pinning where places are, and filling in stops a
/// session's fights don't show.
/// </summary>
/// <remarks>
/// The pages send their &lt;AntiforgeryToken /&gt; with each form, which these check.
/// </remarks>
public static class MapEndpoints
{
    private const int MaxPlaceLength = 80;

    public static RouteGroupBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/map");

        group.MapPost("/places", async ([FromForm] string? name, [FromForm] double x, [FromForm] double y, CampaignMap map, Viewer viewer) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();

            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || !double.IsFinite(x) || !double.IsFinite(y))
                return Results.BadRequest("Say which place, and where.");

            await map.PlaceAsync(name, new BoardSpot(x, y), AuthorOf(viewer));
            return Results.NoContent();
        });

        // A stop the fights don't show, from the session's fill-in page, back to which it goes
        group.MapPost("/sessions/{number:int}/stops", async (
            int number,
            IFormCollection form,
            SessionReader sessions,
            CampaignMap map,
            Viewer viewer) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();
            if (await sessions.GetAsync(number) is not { } session)
                return Results.NotFound();

            var page = EditPage(number);
            var place = form["place"].ToString().Trim();
            if (string.IsNullOrEmpty(place) || place.Length > MaxPlaceLength)
                return DeskForms.Back(page, $"Say where they went, up to {MaxPlaceLength} letters.");
            if (ParseLocal(form["at"]) is not { } when || when < session.Session.StartedAt || when > session.Session.EndedAt)
                return DeskForms.Back(page, "Pick a time during the session.");
            var characters = form["who"].Select(id => ulong.TryParse(id, out var value) ? value : 0).Where(id => id != 0).ToList();
            if (characters.Count == 0)
                return DeskForms.Back(page, "Tick who went.");

            var logs = await map.LogsAsync();
            var stops = logs.GetValueOrDefault(session.Session.ID) ?? [];
            await map.SaveLogAsync(session.Session.ID, [.. stops, new LoggedStop(Guid.NewGuid().ToString("N"), place, when, characters)], AuthorOf(viewer));
            return Results.LocalRedirect(page);
        });

        group.MapPost("/sessions/{number:int}/stops/{id}/delete", async (int number, string id, SessionReader sessions, CampaignMap map, Viewer viewer) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();
            if (await sessions.GetAsync(number) is not { } session)
                return Results.NotFound();

            var stops = (await map.LogsAsync()).GetValueOrDefault(session.Session.ID) ?? [];
            await map.SaveLogAsync(session.Session.ID, [.. stops.Where(s => s.ID != id)], AuthorOf(viewer));
            return Results.LocalRedirect(EditPage(number));
        });

        return group;
    }

    private static string EditPage(int number) => $"/session/{number}/journey/edit";

    private static string AuthorOf(Viewer viewer) => viewer.ID?.ToString() ?? "journey";

    // A datetime-local field's value, which is in the dashboard's own time, as the page shows times
    private static DateTimeOffset? ParseLocal(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local)
            ? new DateTimeOffset(local)
            : null;
}
