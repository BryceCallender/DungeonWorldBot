using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.DungeonWorld;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Rules;

/// <summary>
/// The Druid's Shapeshifter move and the moves that change it.
/// </summary>
public static class ShapeshiftRules
{
    public const string Shapeshifter = "Shapeshifter";
    public const string StudiedEssence = "Studied Essence";
    public const string HoldName = "Shapeshift Hold";
    public const string EmbracingNoForm = "Embracing No Form";
    public const string Chimera = "Chimera";
    public const string Shed = "Shed";
    public const string Formcrafter = "Formcrafter";
    public const string Formshaper = "Formshaper";
    public const string RedOfToothAndClaw = "Red of Tooth and Claw";
    public const string BloodAndThunder = "Blood and Thunder";

    public static ClassMove Move => Druid.Sheet.FindMove(Shapeshifter)!;

    public static ClassResource Hold => Druid.Sheet.FindResource(HoldName)!;

    public static bool Knows(Character character, string move) =>
        ClassSheetLibrary.KnownMoves(character).Contains(move, StringComparer.OrdinalIgnoreCase);

    public static bool CanShapeshift(Character character) => Knows(character, Shapeshifter);

    /// <summary>
    /// How many shapes can be merged into one. Chimera allows three.
    /// </summary>
    public static int MaxShapes(Character character) => Knows(character, Chimera) ? 3 : 1;

    public static int HoldFor(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => 3,
        MoveOutcome.WeakHit => 2,
        _ => 1,
    };

    public static IReadOnlyList<Transformation> Active(Character character) =>
        character.Transformations?.Where(t => t.IsActive).OrderBy(t => t.Name).ToList() ?? [];

    public static Transformation? Find(Character character, string name) =>
        character.Transformations?.FirstOrDefault(t => t.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The current form's name, e.g. "Bear" or "Bear-Eagle-Snake" when merged.
    /// </summary>
    public static string? FormName(Character character) =>
        Active(character) is { Count: > 0 } active ? string.Join('-', active.Select(t => t.Name)) : null;

    /// <summary>
    /// Reminders from the character's moves that apply each time they shapeshift.
    /// </summary>
    public static IEnumerable<string> Reminders(Character character)
    {
        if (Knows(character, Formcrafter))
            yield return "**Formcrafter:** pick a stat for +1 ongoing while shifted. The GM picks one for -1 ongoing.";
        if (Knows(character, Formshaper))
            yield return "**Formshaper:** gain +1 armor or +1d4 damage in this form. Choose one.";
        if (Knows(character, BloodAndThunder))
            yield return "**Blood and Thunder:** in a dangerous animal form, your damage is d10.";
        else if (Knows(character, RedOfToothAndClaw))
            yield return "**Red of Tooth and Claw:** in a dangerous animal form, your damage is d8.";
    }

    /// <summary>
    /// One line for the character sheet: the current form and hold, or natural form.
    /// </summary>
    public static string Status(Character character, string hold) =>
        FormName(character) is { } form ? $"🐾 **{form}** ({HoldName} {hold})" : "Natural form";

    public static string Describe(Transformation form)
    {
        var parts = new List<string>();
        if (form.Description is not null)
            parts.Add(form.Description);
        parts.Add(form.Moves is not null ? $"**Moves:** {form.Moves}" : "*No moves yet. Ask the GM, then add them with /shapeshift edit*");
        if (form.TimesTaken > 0)
            parts.Add($"*Taken {form.TimesTaken} time{(form.TimesTaken == 1 ? string.Empty : "s")}*");
        return string.Join('\n', parts);
    }
}
