using DungeonWorldBot.Data;
using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Encounters;
using DungeonWorldBot.Features.Encounters;
using Microsoft.AspNetCore.Mvc;

namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// Plain form posts for making and putting away canvases, and their pictures. Drawing on them goes through
/// <see cref="CanvasHub"/>.
/// </summary>
/// <remarks>
/// The forms need an &lt;AntiforgeryToken /&gt; inside them, which these check.
/// </remarks>
public static class CanvasEndpoints
{
    public static RouteGroupBuilder MapCanvasEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/canvases");

        // Goes straight to the new canvas, or back to the list if the name won't do
        group.MapPost("/", async ([FromForm] string? name, [FromForm] CanvasKind kind, CanvasStore store, Viewer viewer) =>
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > CanvasStore.MaxNameLength)
                return Results.LocalRedirect("/scenes?error=name");

            var canvas = await store.CreateAsync(name, kind, viewer.ID?.ToString());
            return Results.LocalRedirect($"/scene/{CanvasIds.Encode(canvas.ID)}");
        });

        group.MapPost("/{id}/archive", (string id, CanvasStore store, Viewer viewer) => Archive(id, true, store, viewer));
        group.MapPost("/{id}/restore", (string id, CanvasStore store, Viewer viewer) => Archive(id, false, store, viewer));

        // For good, from the card's menu, which asks first. Back to whichever list it was in
        group.MapPost("/{id}/delete", async (string id, CanvasStore store, Viewer viewer) =>
        {
            if (!CanvasIds.TryDecode(id, out var canvasID) || await store.FindAsync(canvasID) is not { } canvas)
                return Results.NotFound();

            if (!CanPutAway(canvas, viewer))
                return Results.Forbid();

            await store.DeleteAsync(canvasID);
            return Results.LocalRedirect(canvas.ArchivedAt is null ? "/scenes" : "/scenes?archived=true");
        });

        // A picture for the board or a token. The page shrinks it first; this only checks it's really a picture and
        // not too big, and never decodes it.
        group.MapPost("/{id}/images", async (string id, IFormFile file, CanvasStore store, Viewer viewer, IEncounterService encounters) =>
        {
            if (!CanvasIds.TryDecode(id, out var canvasID) || await store.FindAsync(canvasID) is not { } scene)
                return Results.NotFound();
            if (!await FightBoard.CanChangeAsync(scene, viewer, encounters))
                return Results.BadRequest(FightBoard.ViewOnlyMessage);
            if (file.Length is 0 or > MaxImageBytes)
                return Results.BadRequest($"Pictures can be up to {MaxImageBytes / 1024 / 1024} MB.");

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var data = buffer.ToArray();
            if (ImageType(data) is not { } contentType)
                return Results.BadRequest("That isn't a PNG, JPEG, WebP or GIF.");

            var imageID = await store.SaveImageAsync(canvasID, contentType, data, viewer.ID?.ToString());
            return Results.Ok(new { url = $"api/canvases/{id}/images/{imageID}" });
        });

        group.MapGet("/{id}/images/{imageID}", async (string id, string imageID, CanvasStore store, HttpContext context) =>
        {
            if (!CanvasIds.TryDecode(id, out var canvasID) || await store.FindImageAsync(canvasID, imageID) is not { } image)
                return Results.NotFound();

            // An image never changes once it's uploaded, so browsers can keep it. Private, since it needs signing in.
            context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Bytes(image.Data, image.ContentType);
        });

        // The board's picture of itself, for its card. Small, so it's capped well under a full picture.
        group.MapPost("/{id}/thumbnail", async (string id, IFormFile file, CanvasStore store) =>
        {
            if (!CanvasIds.TryDecode(id, out var canvasID) || await store.FindAsync(canvasID) is null)
                return Results.NotFound();
            if (file.Length is 0 or > MaxThumbnailBytes)
                return Results.BadRequest("That's too big for a thumbnail.");

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var data = buffer.ToArray();
            if (ImageType(data) is not { } contentType)
                return Results.BadRequest("That isn't a PNG, JPEG, WebP or GIF.");

            await store.SaveThumbnailAsync(canvasID, contentType, data);
            return Results.NoContent();
        });

        // Linked with ?v= and the time it was taken, so it can be cached for good and still update
        group.MapGet("/{id}/thumbnail", async (string id, CanvasStore store, HttpContext context) =>
        {
            if (!CanvasIds.TryDecode(id, out var canvasID) || await store.FindThumbnailAsync(canvasID) is not { } thumbnail)
                return Results.NotFound();

            context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.Bytes(thumbnail.Data, thumbnail.ContentType);
        });

        return group;
    }

    internal const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxThumbnailBytes = 512 * 1024;

    // What the file's first bytes say it is. SVG isn't allowed: it can carry scripts.
    internal static string? ImageType(ReadOnlySpan<byte> data) =>
        data.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]) ? "image/png"
        : data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? "image/jpeg"
        : data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8) ? "image/gif"
        : data.Length > 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8) ? "image/webp"
        : null;

    // Only the DM or whoever made it can put it away, so nobody clears the board in the middle of a fight
    private static async Task<IResult> Archive(string sqid, bool archived, CanvasStore store, Viewer viewer)
    {
        if (!CanvasIds.TryDecode(sqid, out var id) || await store.FindAsync(id) is not { } canvas)
            return Results.NotFound();

        if (!CanPutAway(canvas, viewer))
            return Results.Forbid();

        await store.SetArchivedAsync(id, archived);
        return Results.LocalRedirect("/scenes");
    }

    private static bool CanPutAway(Canvas canvas, Viewer viewer) =>
        !viewer.SignedIn || viewer.IsDungeonMaster || viewer.Is(canvas.CreatedBy ?? "");
}
