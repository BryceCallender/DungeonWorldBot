using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Rules;

/// <summary>
/// The six Dungeon World debilities, each giving -1 to one stat.
/// </summary>
public enum DebilityType
{
    Weak,
    Shaky,
    Sick,
    Stunned,
    Confused,
    Scarred,
}

public static class DebilityRules
{
    public const int Penalty = -1;

    public static StatType StatFor(DebilityType type) => type switch
    {
        DebilityType.Weak => StatType.Str,
        DebilityType.Shaky => StatType.Dex,
        DebilityType.Sick => StatType.Con,
        DebilityType.Stunned => StatType.Int,
        DebilityType.Confused => StatType.Wis,
        _ => StatType.Cha,
    };

    public static DebilityType For(StatType stat) => Enum.GetValues<DebilityType>().First(d => StatFor(d) == stat);

    /// <summary>
    /// e.g. "Weak (STR)".
    /// </summary>
    public static string Describe(DebilityType type) => $"{type} ({StatFor(type).ToString().ToUpper()})";

    public static IReadOnlyList<DebilityType> Current(Character character) =>
        (character.Debilities ?? []).Select(d => For(d.StatType)).Distinct().OrderBy(d => d).ToList();

    public static bool Has(Character character, DebilityType type) => Current(character).Contains(type);

    /// <summary>
    /// The penalty to rolls with a stat from the character's debilities, unless a move of theirs (like the Slayer's
    /// Tenacity) lets them ignore it.
    /// </summary>
    /// <returns>The debility and its penalty, or null if none applies.</returns>
    public static (DebilityType Debility, int Penalty)? RollPenalty(Character character, StatType stat)
    {
        var debility = For(stat);
        if (!Has(character, debility) || IgnoringMove(character, stat) is not null)
            return null;

        return (debility, Penalty);
    }

    /// <summary>
    /// The character's move that lets them ignore debilities to a stat, if they have one.
    /// </summary>
    public static ClassMove? IgnoringMove(Character character, StatType stat)
    {
        if (ClassSheetLibrary.For(character.Class.Type) is not { } own)
            return null;

        var known = ClassSheetLibrary.KnownMoves(character);
        return own.AllMoves
            .Where(m => known.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
            .Concat(Multiclassing.KnownForeignMoves(character, own).Select(f => f.Move))
            .FirstOrDefault(m => m.IgnoresDebility == stat);
    }
}
