using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Subclasses;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// A roll's embed after changing its outcome.
/// </summary>
/// <param name="Changed">Whether it now counts as something other than what it rolled, or was changed back.</param>
public record OutcomeChange(Embed Embed, bool Changed);

/// <summary>
/// Changes what a posted roll counts as, for the table's special rules the bot doesn't know about (e.g. a blessing that
/// turns a miss into a 10+). The dice stay as rolled: only the result it counts as changes.
/// </summary>
public static class RollOutcomeChange
{
    public const string FieldName = "✏️ Outcome changed";

    private const string OptionsField = "Options";
    private const string DamageField = "Damage";

    private static readonly MoveOutcome[] Outcomes = [MoveOutcome.StrongHit, MoveOutcome.WeakHit, MoveOutcome.Miss];

    /// <summary>
    /// The result a roll's embed shows, or null if it isn't a 10+ / 7-9 / 6- roll.
    /// </summary>
    public static MoveOutcome? Of(IEmbed embed) =>
        Fields(embed).Select(f => OutcomeNamed(f.Name)).FirstOrDefault(o => o is not null);

    /// <summary>
    /// What a roll's embed says it was changed to count as, or null if it wasn't changed.
    /// </summary>
    public static MoveOutcome? ChangedOf(IEmbed embed) =>
        Fields(embed).Any(f => f.Name == FieldName) ? Of(embed) : null;

    /// <summary>
    /// The roll's embed showing the new result, with the move's text for it, its options if it has any then, and who
    /// changed it and why. Changing it back to what was rolled takes the note off again.
    /// </summary>
    /// <remarks>Check <see cref="Of"/> first: this expects a roll with a result.</remarks>
    public static OutcomeChange Apply(IEmbed embed, MoveOutcome outcome, string reason, Snowflake changedBy)
    {
        var fields = Fields(embed).ToList();
        var shown = Of(embed)!.Value;
        var rolled = fields.FirstOrDefault(f => f.Name == FieldName) is { } change ? RolledOf(change.Value) ?? shown : shown;
        var move = MoveOf(embed);

        fields.RemoveAll(f => f.Name is FieldName or OptionsField);
        var index = fields.FindIndex(f => OutcomeNamed(f.Name) is not null);
        fields[index] = new EmbedField(MoveRoller.Describe(outcome), move?.Describe(outcome) ?? MoveRoller.GeneralMeaning(outcome));

        if (move?.ShowsOptions(outcome) == true)
            fields.Insert(index + 1, new EmbedField(OptionsField, string.Join('\n', move.Options!.Select(o => $"• {o}"))));

        if (outcome != rolled)
            fields.Insert(index, new EmbedField(FieldName, Note(rolled, outcome, reason, changedBy, move, fields.Any(f => f.Name == DamageField))));

        return new OutcomeChange(new Embed(
            embed.Title,
            embed.Type,
            embed.Description,
            embed.Url,
            embed.Timestamp,
            MoveRoller.ColourFor(outcome),
            embed.Footer,
            embed.Image,
            embed.Thumbnail,
            embed.Video,
            embed.Provider,
            embed.Author,
            fields), outcome != rolled);
    }

    // Damage only rolls on a hit, so say what's left to do by hand when the change crosses that line
    private static string Note(MoveOutcome rolled, MoveOutcome outcome, string reason, Snowflake changedBy, MoveDefinition? move, bool rolledDamage)
    {
        var note = $"Your {MoveRoller.Describe(rolled)} counts as a {MoveRoller.Describe(outcome)}: {reason}\nChanged by <@{changedBy}>.";
        if (move?.RollsDamage == true && outcome != MoveOutcome.Miss && !rolledDamage)
            note += "\nDamage wasn't rolled. Roll it with /roll damage, and the GM can take it off with /dm encounter damage.";
        else if (outcome == MoveOutcome.Miss && rolledDamage)
            note += "\nThe damage above already landed. The GM can undo it with /dm encounter heal.";
        return note;
    }

    // "Your Miss (6-) counts as..." keeps what was really rolled, however many times it's changed
    private static MoveOutcome? RolledOf(string note) =>
        Outcomes.Cast<MoveOutcome?>().FirstOrDefault(o => note.StartsWith($"Your {MoveRoller.Describe(o!.Value)} "));

    private static MoveOutcome? OutcomeNamed(string name) =>
        Outcomes.Cast<MoveOutcome?>().FirstOrDefault(o => MoveRoller.Describe(o!.Value) == name);

    // Move rolls are titled "🎲 Aria: Hack and Slash"; anything else gets the general meaning of each result
    private static MoveDefinition? MoveOf(IEmbed embed)
    {
        if (!embed.Title.IsDefined(out var title))
            return null;

        var colon = title.IndexOf(": ", StringComparison.Ordinal);
        if (colon < 0)
            return null;

        var name = title[(colon + 2)..].Trim();
        return BasicMoves.All.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
               ?? ClassSheetLibrary.All.Select(s => s.FindMove(name)?.Roll).FirstOrDefault(m => m is not null)
               ?? SubclassLibrary.All.Select(s => s.FindMove(name)?.Roll).FirstOrDefault(m => m is not null);
    }

    private static IReadOnlyList<IEmbedField> Fields(IEmbed embed) => embed.Fields.IsDefined(out var fields) ? fields : [];
}
