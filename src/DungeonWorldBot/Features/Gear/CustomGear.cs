using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Gear;

/// <summary>
/// Something a gear mode keeps count of, like the Ruby's Dodges. Stored like a class resource.
/// </summary>
/// <param name="Max">The most the character can hold, or null for no limit.</param>
/// <param name="GainOnDamage">Whether dealing damage with the gear adds one.</param>
public sealed record GearCounter(string Name, string Summary, int? Max = null, bool GainOnDamage = false)
{
    /// <summary>
    /// Whether ending combat clears it, like Emerald Marks.
    /// </summary>
    public bool ClearsAfterCombat { get; init; }

    public ClassResource Resource { get; } = new(Name, Summary, Start: 0, Max: Max) { ResetWithAll = false };
}

/// <summary>
/// An extra attack a gear mode allows, like the Topaz's Opportunist's Volley.
/// </summary>
/// <param name="HalfDamage">Whether the target takes half the damage rolled, rounded up.</param>
public sealed record GearAction(string Name, string Trigger, bool HalfDamage = false);

/// <summary>
/// One setting a piece of gear can be in, like a gem socketed in the Gem-Aspected Bow. Only one is active at a time.
/// </summary>
public sealed record GearMode(string Name, string Title, string Effect, string Visual)
{
    /// <summary>
    /// A reminder shown with every damage roll made with the gear, like the Diamond ignoring armor.
    /// </summary>
    public string? DamageNote { get; init; }

    /// <summary>
    /// Whether one damage die (the lowest) is rerolled, keeping the higher result.
    /// </summary>
    public bool RerollLowestDamageDie { get; init; }

    public GearCounter? Counter { get; init; }

    public GearAction? Action { get; init; }
}

/// <summary>
/// A homebrew item with its own rules, like the Gem-Aspected Bow. Owning it is tracked as an ability named after it,
/// whose description records the active mode.
/// </summary>
/// <param name="ModeKind">What a mode is called on this item, e.g. "Gem".</param>
/// <param name="UsedFor">The moves whose damage the gear applies to, e.g. Volley for a bow. Empty for every damage roll.</param>
public sealed record CustomGear(
    string Name,
    string Summary,
    string ModeKind,
    IReadOnlyList<GearMode> Modes,
    IReadOnlyList<string> UsedFor)
{
    /// <summary>
    /// A reminder about changing modes, like "Replacing gems is an extra action on your turn".
    /// </summary>
    public string? SwapNote { get; init; }

    public bool AppliesTo(string move) => UsedFor.Count == 0 || UsedFor.Contains(move, StringComparer.OrdinalIgnoreCase);

    public GearMode? FindMode(string name) =>
        Modes.FirstOrDefault(m => m.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A piece of custom gear a character has, and the mode it's in (null if none is set).
/// </summary>
public sealed record OwnedGear(CustomGear Gear, GearMode? Mode);

/// <summary>
/// A damage die that was rerolled.
/// </summary>
public sealed record Reroll(int Sides, int Before, int Rolled)
{
    /// <summary>
    /// The result kept: whichever was higher.
    /// </summary>
    public int Kept => Math.Max(Before, Rolled);

    public int Gain => Kept - Before;
}

public static class GearRules
{
    /// <summary>
    /// The ability description for a piece of gear in a mode.
    /// </summary>
    public static string Describe(CustomGear gear, GearMode? mode) =>
        $"{gear.Summary}\n**{gear.ModeKind}:** " + (mode is null ? "none" : $"{mode.Name} ({mode.Title})");

    /// <summary>
    /// The custom gear the character has.
    /// </summary>
    public static IReadOnlyList<OwnedGear> Owned(Character character) =>
        GearLibrary.All
            .Select(g => (Gear: g, Ability: character.Abilities?.FirstOrDefault(a => a.Name.Equals(g.Name, StringComparison.OrdinalIgnoreCase))))
            .Where(g => g.Ability is not null)
            .Select(g => new OwnedGear(g.Gear, ModeFrom(g.Gear, g.Ability!.Description)))
            .ToList();

    // The mode is on the description's "**Gem:** Ruby (Aspect of Evasion)" line
    private static GearMode? ModeFrom(CustomGear gear, string? description)
    {
        var prefix = $"**{gear.ModeKind}:** ";
        var line = description?.Split('\n').LastOrDefault(l => l.StartsWith(prefix, StringComparison.Ordinal));
        return line is null ? null : gear.FindMode(line[prefix.Length..].Split(" (")[0]);
    }

    /// <summary>
    /// Rerolls the lowest die, keeping whichever result is higher.
    /// </summary>
    /// <param name="dice">Each die's sides and result.</param>
    /// <param name="roll">Rolls a die with the given number of sides.</param>
    /// <returns>The reroll, or null if there were no dice.</returns>
    public static Reroll? RerollLowest(IReadOnlyList<(int Sides, int Value)> dice, Func<int, int> roll)
    {
        if (dice.Count == 0)
            return null;

        var (sides, value) = dice.MinBy(d => d.Value);
        return new Reroll(sides, value, roll(sides));
    }

    /// <summary>
    /// Half of a damage roll, rounded up.
    /// </summary>
    public static int Half(int damage) => (Math.Max(0, damage) + 1) / 2;
}
