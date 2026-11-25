using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Dashboard.Models.Party;
using DungeonWorldBot.Data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DungeonWorldBot.Dashboard.Services.Party;

/// <summary>
/// The Roll buttons on a character's sheet (see <see cref="SheetRolls"/>): a form post asks the bot to roll a move, and
/// the sheet asks after it until the bot has. app.js sends them, with the sheet's antiforgery token.
/// </summary>
public static class SheetRollEndpoints
{
    public static RouteGroupBuilder MapSheetRollEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/characters/{id}/rolls");

        // The stat comes as its short name in any case, "dex" as the board has it or "Dex"
        group.MapPost("", async (ulong id, [FromForm] string? move, [FromForm] bool classMove, [FromForm] string? stat,
            [FromForm] ulong? ally, [FromForm] int? bonus, SheetRolls rolls, Viewer viewer) =>
        {
            if (!await rolls.CanRollAsync(viewer, id))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(move))
                return Results.BadRequest(MoveAsked.Failed("Pick a move to roll."));

            StatType? picked = Enum.TryParse<StatType>(stat, ignoreCase: true, out var parsed) ? parsed : null;
            var asked = await rolls.AskAsync(id, viewer, new SheetRoll(move, classMove, picked, ally, bonus ?? 0));
            return asked.Error is null ? Results.Ok(asked) : Results.BadRequest(asked);
        });

        group.MapGet("/{requestID:int}", async (ulong id, int requestID, SheetRolls rolls, Viewer viewer) =>
        {
            if (!await rolls.CanRollAsync(viewer, id))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            return await rolls.StatusAsync(id, requestID) is { } status ? Results.Ok(status) : Results.NotFound();
        });

        return group;
    }
}
