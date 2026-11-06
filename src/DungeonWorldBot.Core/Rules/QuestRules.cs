using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.DungeonWorld;

namespace DungeonWorldBot.Rules;

/// <summary>
/// A boon a Paladin can choose for their quest.
/// </summary>
/// <param name="Blank">What the player fills in, e.g. "what it leads to", or null when there's nothing to fill.</param>
public record QuestBoon(string Name, string Summary, string? Blank = null);

/// <summary>
/// The Paladin's Quest: the mission, its boons and vows, and the moves that only work while on one.
/// </summary>
/// <remarks>
/// The mission is kept as the Quest ability's description, and each boon and vow as an ability of its own, so a quest
/// shows on the character sheet like any other move.
/// </remarks>
public static class QuestRules
{
    public const string Quest = "Quest";
    public const string BoonPrefix = "Boon: ";
    public const string VowPrefix = "Vow: ";

    /// <summary>
    /// Starts the Quest ability's description while on one, e.g. "On a quest: Slay the Wyrm of Ashfall".
    /// </summary>
    public const string MissionPrefix = "On a quest: ";

    public const string Smite = "Smite";
    public const string HolySmite = "Holy Smite";
    public const string HolyProtection = "Holy Protection";
    public const string DivineProtection = "Divine Protection";
    public const string PerfectKnight = "Perfect Knight";

    /// <summary>
    /// Where the armor from Holy or Divine Protection comes from, so ending the quest can take it away.
    /// </summary>
    public const string ArmorSource = "Quest";

    public const int Boons = 2;
    public const int PerfectKnightBoons = 3;

    public static IReadOnlyList<QuestBoon> AllBoons { get; } =
    [
        new("Unwavering Direction", "An unwavering sense of direction to {0}.", "where it leads"),
        new("Invulnerability", "Invulnerability to {0}.", "what can't hurt you, e.g. edged weapons, fire, enchantment"),
        new("Divine Authority", "A mark of divine authority."),
        new("Piercing Senses", "Senses that pierce lies."),
        new("Transcendent Voice", "A voice that transcends language."),
        new("Freedom from Want", "Freedom from hunger, thirst and sleep."),
    ];

    public static IReadOnlyList<string> AllVows { get; } = ["Honor", "Temperance", "Piety", "Valor", "Truth", "Hospitality"];

    public static ClassMove QuestMove => Paladin.Sheet.FindMove(Quest)!;

    public static Ability? QuestAbility(Character character) =>
        character.Abilities.FirstOrDefault(a => a.Name.Equals(Quest, StringComparison.OrdinalIgnoreCase));

    public static string? Mission(Character character) =>
        QuestAbility(character)?.Description is { } description && description.StartsWith(MissionPrefix)
            ? description[MissionPrefix.Length..]
            : null;

    public static bool IsOnQuest(Character character) => Mission(character) is not null;

    public static int MaxBoons(Character character) => ClassSheetLibrary.Knows(character, PerfectKnight) ? PerfectKnightBoons : Boons;

    public static QuestBoon? FindBoon(string name) =>
        AllBoons.FirstOrDefault(b => b.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? FindVow(string name) =>
        AllVows.FirstOrDefault(v => v.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<Ability> BoonsOf(Character character) => WithPrefix(character, BoonPrefix);

    public static IReadOnlyList<Ability> VowsOf(Character character) => WithPrefix(character, VowPrefix);

    /// <summary>
    /// What the boon gives, with the player's blank filled in.
    /// </summary>
    public static string Describe(QuestBoon boon, string? detail) =>
        string.Format(boon.Summary, string.IsNullOrWhiteSpace(detail) ? "___" : detail.Trim());

    /// <summary>
    /// The extra damage die from Smite (1d4) or Holy Smite (1d8), while on a quest.
    /// </summary>
    public static string? SmiteDamage(Character character)
    {
        if (!IsOnQuest(character))
            return null;
        if (ClassSheetLibrary.Knows(character, HolySmite))
            return "1d8";
        return ClassSheetLibrary.Knows(character, Smite) ? "1d4" : null;
    }

    /// <summary>
    /// The armor from Holy Protection (+1) or Divine Protection (+2) while on a quest.
    /// </summary>
    public static int QuestArmor(Character character)
    {
        if (ClassSheetLibrary.Knows(character, DivineProtection))
            return 2;
        return ClassSheetLibrary.Knows(character, HolyProtection) ? 1 : 0;
    }

    /// <summary>
    /// The quest for the character sheet: mission, boons and vows.
    /// </summary>
    public static string Describe(Character character)
    {
        if (Mission(character) is not { } mission)
            return "Not on a quest.";

        List<string> lines = [$"**Mission:** {mission}"];
        lines.AddRange(BoonsOf(character).Select(b => $"✨ **{b.Name[BoonPrefix.Length..]}:** {b.Description}"));

        var vows = VowsOf(character);
        lines.Add(vows.Count > 0
            ? $"**Vows:** {string.Join(", ", vows.Select(v => v.Name[VowPrefix.Length..]))}"
            : "*No vows yet. The GM sets them; record them with /quest vow*");
        return string.Join('\n', lines);
    }

    private static IReadOnlyList<Ability> WithPrefix(Character character, string prefix) =>
        character.Abilities.Where(a => a.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(a => a.Name).ToList();
}
