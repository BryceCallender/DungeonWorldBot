using DungeonWorldBot.Dashboard.Models.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DungeonWorldBot.Dashboard.Services.Party;

/// <summary>
/// Plain form posts for a character's backstory, goals and notes. Only the character's own player can change them,
/// so nobody (the DM included) writes someone else's story.
/// </summary>
/// <remarks>
/// The forms need an &lt;AntiforgeryToken /&gt; inside them, which these check.
/// </remarks>
public static class CharacterJournalEndpoints
{
    public static RouteGroupBuilder MapCharacterJournalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/characters/{id}");

        group.MapPost("/backstory", (ulong id, [FromForm] string? text, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, text is null || text.Length <= CharacterJournal.MaxBackstoryLength, () => journal.SetBackstoryAsync(id, text)));

        group.MapPost("/goals", (ulong id, [FromForm] string? text, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, Fits(text, CharacterJournal.MaxGoalLength), () => journal.AddGoalAsync(id, text!)));

        group.MapPost("/goals/{goalID:int}", (ulong id, int goalID, [FromForm] string? text, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, Fits(text, CharacterJournal.MaxGoalLength), () => journal.EditGoalAsync(id, goalID, text!)));

        group.MapPost("/goals/{goalID:int}/complete", (ulong id, int goalID, [FromForm] bool completed, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, true, () => journal.SetGoalCompletedAsync(id, goalID, completed)));

        group.MapPost("/goals/{goalID:int}/remove", (ulong id, int goalID, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, true, () => journal.RemoveGoalAsync(id, goalID)));

        group.MapPost("/notes", (ulong id, [FromForm] string? text, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, Fits(text, CharacterJournal.MaxTextLength), () => journal.AddNoteAsync(id, text!)));

        group.MapPost("/notes/{noteID:int}", (ulong id, int noteID, [FromForm] string? text, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, Fits(text, CharacterJournal.MaxTextLength), () => journal.EditNoteAsync(id, noteID, text!)));

        group.MapPost("/notes/{noteID:int}/remove", (ulong id, int noteID, [FromForm] string? back, CharacterJournal journal, Viewer viewer) =>
            Write(id, viewer, back, true, () => journal.RemoveNoteAsync(id, noteID)));

        return group;
    }

    private static bool Fits(string? text, int max) =>
        !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= max;

    private static async Task<IResult> Write(ulong id, Viewer viewer, string? back, bool valid, Func<Task<bool>> write)
    {
        // Your character shares your Discord ID, so this is "is it yours?". Without sign-in it's nobody's.
        if (!viewer.Is(id))
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!valid)
            return Results.BadRequest("That's too long, or empty.");
        if (!await write())
            return Results.NotFound();

        // Back to the page (and section) the form was on
        return Results.LocalRedirect(back is { Length: > 0 } && back.StartsWith('/') && !back.StartsWith("//") && !back.StartsWith("/\\") ? back : "/");
    }
}
