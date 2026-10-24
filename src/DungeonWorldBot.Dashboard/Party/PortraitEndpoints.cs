using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Dashboard.Encounters;

namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// A character's picture: setting it from their page, taking it off, and serving it for the page and their tokens.
/// </summary>
/// <remarks>
/// The forms need an &lt;AntiforgeryToken /&gt; inside them, which these check.
/// </remarks>
public static class PortraitEndpoints
{
    public static RouteGroupBuilder MapPortraitEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/characters/{id}/portrait");

        group.MapGet("/", async (ulong id, PortraitStore store, HttpContext context) =>
        {
            if (await store.FindAsync(id) is not { } portrait)
                return Results.NotFound();

            // A new picture gets a new address (see PortraitStore.Url), so browsers can keep this one. Private, since
            // it needs signing in.
            context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Bytes(portrait.Data, portrait.ContentType);
        });

        group.MapPost("/", async (ulong id, IFormFile picture, PortraitStore store, PartyReader party, Viewer viewer) =>
        {
            if (!CanChange(id, viewer))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (await party.GetCharacterAsync(id) is not { } character)
                return Results.NotFound();
            if (picture.Length is 0 or > CanvasEndpoints.MaxImageBytes)
                return Results.BadRequest($"Pictures can be up to {CanvasEndpoints.MaxImageBytes / 1024 / 1024} MB.");

            using var buffer = new MemoryStream();
            await picture.CopyToAsync(buffer);
            var data = buffer.ToArray();
            if (CanvasEndpoints.ImageType(data) is not { } contentType)
                return Results.BadRequest("That isn't a PNG, JPEG, WebP or GIF.");

            await store.SaveAsync(id, contentType, data);
            return Results.LocalRedirect("/" + character.Url);
        });

        group.MapPost("/remove", async (ulong id, PortraitStore store, PartyReader party, Viewer viewer) =>
        {
            if (!CanChange(id, viewer))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (await party.GetCharacterAsync(id) is not { } character)
                return Results.NotFound();

            await store.RemoveAsync(id);
            return Results.LocalRedirect("/" + character.Url);
        });

        return group;
    }

    /// <summary>
    /// Who can set a character's picture: their own player, or the DM (anyone, when sign-in is off).
    /// </summary>
    /// <param name="id">The character's ID, which is their player's Discord ID.</param>
    public static bool CanChange(ulong id, Viewer viewer) => viewer.Is(id) || EncounterEndpoints.CanRun(viewer);
}
