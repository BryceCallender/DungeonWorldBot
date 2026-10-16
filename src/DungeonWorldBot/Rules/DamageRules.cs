using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Rules;

/// <summary>
/// The result of a character taking a hit.
/// </summary>
/// <param name="Damage">The damage dealt, before armor.</param>
/// <param name="Armor">The character's armor against this type of damage.</param>
/// <param name="Taken">The HP actually lost.</param>
/// <param name="Broken">Armor that only lasted until the character was hit, now gone.</param>
public sealed record DamageTaken(int Damage, DamageType Type, int Armor, int Piercing, int Taken, IReadOnlyList<ArmorBonus> Broken);

public static class DamageRules
{
    /// <summary>
    /// The armor that reduces this type of damage, or null for damage that ignores armor.
    /// </summary>
    public static ArmorType? ArmorFor(DamageType type) => type switch
    {
        DamageType.Physical => ArmorType.Physical,
        DamageType.Magical => ArmorType.Magical,
        _ => null,
    };

    /// <summary>
    /// The character's own armor of this type, without temporary armor.
    /// </summary>
    public static int BaseArmor(Character character, ArmorType type) =>
        type == ArmorType.Physical ? character.ArmorRating : character.MagicArmor;

    /// <summary>
    /// The character's own armor plus their temporary armor of this type, never below 0.
    /// </summary>
    public static int Armor(Character character, ArmorType type) =>
        Math.Max(0, BaseArmor(character, type) + (character.ArmorBonuses?.Where(b => b.Type == type).Sum(b => b.Amount) ?? 0));

    public static int ArmorAgainst(Character character, DamageType type) =>
        ArmorFor(type) is { } armor ? Armor(character, armor) : 0;

    /// <summary>
    /// The HP lost to a hit: damage minus armor, where piercing ignores that much armor.
    /// </summary>
    public static int Taken(int damage, int armor, int piercing) =>
        Math.Max(0, damage - Math.Max(0, armor - piercing));

    /// <summary>
    /// How the hit was worked out, e.g. "9 magical − 2 magical armor = **7**".
    /// </summary>
    public static string Describe(DamageTaken hit)
    {
        if (ArmorFor(hit.Type) is not { } armorType)
            return $"{hit.Damage} true damage, ignoring armor = **{hit.Taken}**";

        var type = Describe(armorType);
        if (hit.Armor == 0)
            return $"{hit.Damage} {type}, no {type} armor = **{hit.Taken}**";

        var armor = hit.Piercing > 0
            ? $"({hit.Armor} {type} armor − {hit.Piercing} piercing)"
            : $"{hit.Armor} {type} armor";
        return $"{hit.Damage} {type} − {armor} = **{hit.Taken}**";
    }

    public static string Describe(ArmorType type) => type == ArmorType.Physical ? "physical" : "magical";

    public static string Describe(ArmorDuration duration) => duration switch
    {
        ArmorDuration.Fight => "until the fight ends",
        ArmorDuration.UntilHit => "until hit",
        _ => "until removed",
    };

    /// <summary>
    /// e.g. "+1 magical from Lightning barrier (until hit)".
    /// </summary>
    public static string Describe(ArmorBonus bonus) =>
        $"{(bonus.Amount >= 0 ? "+" : "")}{bonus.Amount} {Describe(bonus.Type)} from {bonus.Source} ({Describe(bonus.Duration)})";

    /// <summary>
    /// e.g. "Physical 2 · Magical 1", including temporary armor.
    /// </summary>
    public static string Totals(Character character) =>
        $"Physical {Armor(character, ArmorType.Physical)} · Magical {Armor(character, ArmorType.Magical)}";

    /// <summary>
    /// The character's armor of each type, then each piece of temporary armor on its own line.
    /// </summary>
    public static string Summary(Character character)
    {
        var summary = Totals(character);
        if (character.ArmorBonuses is { Count: > 0 } bonuses)
            summary += string.Concat(bonuses.Select(b => $"\n• {Describe(b)}"));
        return summary;
    }
}
