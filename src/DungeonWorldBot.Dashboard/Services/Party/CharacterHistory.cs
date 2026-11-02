using DungeonWorldBot.Dashboard.Models.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Sessions;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Party;

/// <summary>
/// A character's timeline: what changed on them (see <see cref="CharacterEvent"/>), their most memorable rolls, the
/// fights they were in and the notes they wrote, in chapters by session.
/// </summary>
/// <remarks>
/// Changes are only kept from when the bot started recording them, but rolls, fights and notes go back further, so
/// older chapters are thinner rather than missing.
/// </remarks>
public class CharacterHistory(DungeonWorldContext context, ISessionService sessions)
{
    // Moves the bot adds and takes away to keep track of something (a vow, an aspect, a tactic), not ones learned
    private static readonly HashSet<string> Bookkeeping = new(
        SubclassLibrary.All.SelectMany(s => s.StateAbilities).Append(TacticRules.ActiveTactic),
        StringComparer.OrdinalIgnoreCase);

    public async Task<List<HistoryChapter>> GetAsync(Character character)
    {
        var id = character.ID;
        List<HistoryEntry> entries = [];

        entries.AddRange((await EventsAsync(character)).Select(Describe).OfType<HistoryEntry>());

        var rolls = await context.Rolls.AsNoTracking().Where(r => r.UserID == id).ToListAsync();
        entries.AddRange(rolls.Select(Describe).OfType<HistoryEntry>());

        var fights = await context.EncounterParticipants.AsNoTracking()
            .Where(p => p.CharacterID == id)
            .Select(p => p.Encounter)
            .ToListAsync();
        var actions = await context.EncounterActions.AsNoTracking()
            .Where(a => a.CharacterID == id)
            .GroupBy(a => a.EncounterID)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count);
        entries.AddRange(fights.DistinctBy(f => f.ID).Select(f => new HistoryEntry(
            f.StartedAt,
            HistoryKind.Fight,
            $"Fought: {f.Name}",
            actions.GetValueOrDefault(f.ID) switch { 0 => null, 1 => "1 action", var n => $"{n} actions" },
            $"fight/{f.ID}")));

        entries.AddRange((character.Notes ?? []).Select(n => new HistoryEntry(
            new DateTimeOffset(DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc)),
            HistoryKind.Note,
            "Wrote a note",
            n.Text.Length > 200 ? n.Text[..200].TrimEnd() + "…" : n.Text)));

        return await ChaptersAsync(entries);
    }

    // In sessions where they were played, and by day in between, newest first
    private async Task<List<HistoryChapter>> ChaptersAsync(List<HistoryEntry> entries)
    {
        List<GameSession> all;
        try
        {
            all = await sessions.GetAllAsync();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table"))
        {
            all = [];
        }

        return entries
            .GroupBy(e => all.FindIndex(s => e.At >= s.StartedAt && e.At <= (s.EndedAt ?? DateTimeOffset.MaxValue)) is var i and >= 0
                ? $"session:{i}"
                : $"day:{e.At.ToLocalTime().Date:yyyy-MM-dd}")
            .Select(g =>
            {
                var newest = g.OrderByDescending(e => e.At).ToList();
                var start = newest[^1].At;
                if (!g.Key.StartsWith("session:"))
                    return new HistoryChapter($"Between sessions · {start.ToLocalTime():ddd d MMM yyyy}", null, start, newest);

                var number = int.Parse(g.Key["session:".Length..]) + 1;
                var session = all[number - 1];
                var title = session.Title is { } t ? $"Session {number}: {t}" : $"Session {number}";
                return new HistoryChapter(title, $"session/{number}", session.StartedAt, newest);
            })
            .OrderByDescending(c => c.Start)
            .ToList();
    }

    private async Task<List<CharacterEvent>> EventsAsync(Character character)
    {
        try
        {
            return await context.CharacterEvents.AsNoTracking().Where(e => e.CharacterID == character.ID).ToListAsync();
        }
        // Before the bot's database had them
        catch (SqliteException e) when (e.Message.Contains("no such table"))
        {
            return [];
        }
    }

    private static HistoryEntry? Describe(CharacterEvent e)
    {
        var subject = e.Subject ?? "";
        (HistoryKind Kind, string Text)? entry = e.Kind switch
        {
            CharacterEventKind.Created => (HistoryKind.Milestone, "Joined the party"),
            CharacterEventKind.LevelChanged when e.To > e.From => (HistoryKind.Level, $"Reached level {e.To}"),
            CharacterEventKind.LevelChanged => (HistoryKind.Level, $"Went back to level {e.To}"),
            CharacterEventKind.StatChanged => (HistoryKind.Stat, $"{subject.ToUpper()} {e.From} → {e.To}"),
            CharacterEventKind.MaxHPChanged => (HistoryKind.Stat, $"Max HP {e.From} → {e.To}"),
            CharacterEventKind.ClassChanged => (HistoryKind.Milestone, $"Became a {subject}"),
            CharacterEventKind.MoveLearned or CharacterEventKind.MoveForgotten when Bookkeeping.Contains(subject) => null,
            CharacterEventKind.MoveLearned when SubclassLibrary.Find(subject) is not null => (HistoryKind.Milestone, $"Took up the {subject}"),
            CharacterEventKind.MoveForgotten when SubclassLibrary.Find(subject) is not null => (HistoryKind.Milestone, $"Left the {subject}"),
            CharacterEventKind.MoveLearned => (HistoryKind.Move, $"Learned {subject}"),
            CharacterEventKind.MoveForgotten => (HistoryKind.Move, $"Gave up {subject}"),
            CharacterEventKind.DebilityTaken => (HistoryKind.Hurt, $"Became {Debility(subject)}"),
            CharacterEventKind.DebilityHealed => (HistoryKind.Healed, $"No longer {Debility(subject)}"),
            CharacterEventKind.BondMade => (HistoryKind.Bond, $"Made a bond with {subject}"),
            CharacterEventKind.BondResolved => (HistoryKind.Bond, $"Resolved a bond with {subject}"),
            CharacterEventKind.GoalSet => (HistoryKind.Goal, $"Set a goal: {subject}"),
            CharacterEventKind.GoalCompleted => (HistoryKind.Goal, $"Completed a goal: {subject}"),
            CharacterEventKind.ItemGained when e.To > 1 => (HistoryKind.Item, $"Got {subject} ×{e.To}"),
            CharacterEventKind.ItemGained => (HistoryKind.Item, $"Got {subject}"),
            CharacterEventKind.ItemLost => (HistoryKind.Item, $"Lost {subject}"),
            CharacterEventKind.CoinsChanged when e.To > e.From => (HistoryKind.Coins, $"Gained {e.To - e.From} coins"),
            CharacterEventKind.CoinsChanged => (HistoryKind.Coins, $"Spent {e.From - e.To} coins"),
            CharacterEventKind.ShapeLearned => (HistoryKind.Move, $"Learned the shape of {subject}"),
            CharacterEventKind.Died => (HistoryKind.Hurt, "Died"),
            CharacterEventKind.Revived => (HistoryKind.Healed, "Came back from death"),
            _ => null,
        };

        var detail = e.Kind == CharacterEventKind.CoinsChanged ? $"{e.To} now" : null;
        return entry is { } found ? new HistoryEntry(e.At, found.Kind, found.Text, detail) : null;
    }

    // Only the rolls worth remembering: double sixes, snake eyes, Last Breath, and those the table changed
    private static HistoryEntry? Describe(RollRecord r)
    {
        var at = r.MessageID.Timestamp;
        var move = r.Move is { } m ? $" on {m}" : "";
        var dice = $"{r.First} + {r.Second}{(r.Modifier == 0 ? "" : $" {(r.Modifier > 0 ? "+" : "−")} {Math.Abs(r.Modifier)}")} = {r.Total}";

        if (string.Equals(r.Move, "Last Breath", StringComparison.OrdinalIgnoreCase))
        {
            var outcome = r.ChangedOutcome ?? (r.Total >= 10 ? MoveOutcome.StrongHit : r.Total >= 7 ? MoveOutcome.WeakHit : MoveOutcome.Miss);
            var text = outcome switch
            {
                MoveOutcome.StrongHit => "Took their Last Breath, and walked away",
                MoveOutcome.WeakHit => "Took their Last Breath, and struck a bargain with Death",
                _ => "Took their Last Breath, and Death took them",
            };
            return new HistoryEntry(at, HistoryKind.Hurt, text, dice);
        }
        if (r.ChangedOutcome is { } changed)
            return new HistoryEntry(at, HistoryKind.Roll, $"Roll{move} turned into a {Name(changed)}", r.ChangedReason ?? dice);
        if (r is { First: 6, Second: 6 })
            return new HistoryEntry(at, HistoryKind.Roll, $"Rolled double sixes{move}", dice);
        if (r is { First: 1, Second: 1 })
            return new HistoryEntry(at, HistoryKind.Roll, $"Rolled snake eyes{move}", dice);
        return null;
    }

    private static string Name(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => "strong hit",
        MoveOutcome.WeakHit => "weak hit",
        _ => "miss",
    };

    // "Str" → "Weak (STR)"
    private static string Debility(string stat) =>
        Enum.TryParse<StatType>(stat, out var type) ? DebilityRules.Describe(DebilityRules.For(type)) : stat;
}
