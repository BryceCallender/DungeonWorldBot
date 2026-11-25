using DungeonWorldBot.Dashboard.Components;
using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Bridge;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DungeonWorldBot.Dashboard.Services.Bridge;

/// <summary>
/// The bot's commands from the dashboard (see <see cref="CommandDesk"/>), for commands.js: what can be run, what
/// someone can do right now, running one, values suggested for an option, and how each went.
/// </summary>
/// <remarks>
/// Runs and suggestions are form posts with the page's antiforgery token. Each option comes as a field named "o." and
/// the option's name, e.g. "o.merge-with".
/// </remarks>
public static class CommandEndpoints
{
    private const string OptionPrefix = "o.";

    public static RouteGroupBuilder MapCommandEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commands");

        group.MapGet("/", (Viewer viewer, CommandDesk desk) => desk.MenuAsync(viewer));

        group.MapPost("/run", async (IFormCollection form, Viewer viewer, CommandDesk desk) =>
            Answer(AskOf(form) is { } ask ? await desk.RunAsync(viewer, ask) : Unreadable));

        group.MapPost("/suggest", async (IFormCollection form, Viewer viewer, CommandDesk desk) =>
            Answer(AskOf(form) is { } ask ? await desk.SuggestAsync(viewer, ask) : Unreadable));

        // What someone can do right now, for the Play button on every page
        group.MapGet("/quick/{who}", (ulong who, Viewer viewer) =>
            viewer.CanActAs(who)
                ? (IResult)new RazorComponentResult<QuickActionsPanel>(new { As = who })
                : Results.StatusCode(StatusCodes.Status403Forbidden));

        group.MapGet("/{id:int}", async (int id, Viewer viewer, CommandDesk desk) =>
            await desk.StatusAsync(viewer, id) is { } status ? Results.Ok(status) : Results.NotFound());

        return group;
    }

    private static readonly CommandAsked Unreadable = CommandAsked.Failed("Pick a command and who it's for.");

    private static IResult Answer(CommandAsked asked) => asked.Error is null ? Results.Ok(asked) : Results.BadRequest(asked);

    private static CommandAsk? AskOf(IFormCollection form)
    {
        if (form["command"].ToString() is not { Length: > 0 } command || !ulong.TryParse(form["as"], out var who))
            return null;

        var options = form.Keys
            .Where(k => k.StartsWith(OptionPrefix, StringComparison.Ordinal))
            .ToDictionary(k => k[OptionPrefix.Length..], k => form[k].ToString());
        return new CommandAsk(command, who, options, form["focused"].ToString() is { Length: > 0 } focused ? focused : null);
    }
}
