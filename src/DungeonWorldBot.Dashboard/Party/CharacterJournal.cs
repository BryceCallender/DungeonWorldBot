using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// The one part of the bot's database the dashboard writes: a character's backstory, notes and goals, which players
/// keep here rather than in Discord. Everything else stays read-only.
/// </summary>
public sealed class CharacterJournal
{
    public const int MaxTextLength = 4000;

    /// <summary>
    /// Backstories can run to several pages, so they get more room than a note.
    /// </summary>
    public const int MaxBackstoryLength = 20000;
    public const int MaxGoalLength = 200;

    private readonly DbContextOptions<DungeonWorldContext> _options;

    /// <param name="options">Options for opening the bot's database read-write.</param>
    public CharacterJournal(DbContextOptions<DungeonWorldContext> options)
    {
        _options = options;
    }

    public Task<bool> SetBackstoryAsync(ulong characterID, string? text) =>
        ChangeAsync(characterID, c => c.Backstory = Text(text));

    public Task<bool> AddGoalAsync(ulong characterID, string text) =>
        ChangeAsync(characterID, c => (c.Goals ??= []).Add(new Goal { Text = text.Trim() }));

    public Task<bool> EditGoalAsync(ulong characterID, int goalID, string text) =>
        ChangeGoalAsync(characterID, goalID, (_, g) => g.Text = text.Trim());

    public Task<bool> SetGoalCompletedAsync(ulong characterID, int goalID, bool completed) =>
        ChangeGoalAsync(characterID, goalID, (_, g) => g.Completed = completed);

    public Task<bool> RemoveGoalAsync(ulong characterID, int goalID) =>
        ChangeGoalAsync(characterID, goalID, (c, g) => c.Goals!.Remove(g));

    public Task<bool> AddNoteAsync(ulong characterID, string text) =>
        ChangeAsync(characterID, c => (c.Notes ??= []).Add(new Note { Text = text.Trim(), CreatedAt = DateTime.UtcNow }));

    public Task<bool> EditNoteAsync(ulong characterID, int noteID, string text) =>
        ChangeNoteAsync(characterID, noteID, (_, n) => n.Text = text.Trim());

    public Task<bool> RemoveNoteAsync(ulong characterID, int noteID) =>
        ChangeNoteAsync(characterID, noteID, (c, n) => c.Notes!.Remove(n));

    // Blank means none, so an emptied box clears it
    private static string? Text(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private Task<bool> ChangeGoalAsync(ulong characterID, int goalID, Action<Character, Goal> change) =>
        ChangeAsync(characterID, c =>
        {
            if (c.Goals?.FirstOrDefault(g => g.ID == goalID) is not { } goal)
                return false;

            change(c, goal);
            return true;
        });

    private Task<bool> ChangeNoteAsync(ulong characterID, int noteID, Action<Character, Note> change) =>
        ChangeAsync(characterID, c =>
        {
            if (c.Notes?.FirstOrDefault(n => n.ID == noteID) is not { } note)
                return false;

            change(c, note);
            return true;
        });

    private Task<bool> ChangeAsync(ulong characterID, Action<Character> change) =>
        ChangeAsync(characterID, c =>
        {
            change(c);
            return true;
        });

    // A fresh context each time, so a write never picks up anything else and is done before the page reloads
    private async Task<bool> ChangeAsync(ulong characterID, Func<Character, bool> change)
    {
        await using var db = new DungeonWorldContext(_options);
        var id = new Snowflake(characterID);
        var character = await db.Characters.Include(c => c.Goals).Include(c => c.Notes).FirstOrDefaultAsync(c => c.ID == id);
        if (character is null || !change(character))
            return false;

        await db.SaveChangesAsync();
        return true;
    }
}
