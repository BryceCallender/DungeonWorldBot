using DungeonWorldBot.Dashboard.Helpers;
using DungeonWorldBot.Dashboard.Models.Auth;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// Deleting a row from the admin pages, behind its confirm dialog. Saving goes through the row page's own form, which
/// keeps what was typed when the database says no.
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin");

        group.MapPost("/{database}/{table}/{rowID:long}/delete", async (string database, string table, long rowID, Viewer viewer, AdminDesk desk) =>
        {
            if (!viewer.CanRunGame)
                return Results.Forbid();
            if (AdminFormat.Parse(database) is not { } which)
                return Results.NotFound();

            var url = "/" + AdminFormat.TableUrl(which, table);
            return await desk.DeleteAsync(which, table, rowID) is { } error
                ? Results.LocalRedirect($"/{AdminFormat.RowUrl(which, table, rowID)}?error={Uri.EscapeDataString(error)}")
                : Results.LocalRedirect($"{url}?saved={Uri.EscapeDataString($"Deleted row {rowID}.")}");
        });

        return group;
    }
}
