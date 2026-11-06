using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.GrimWorld;

namespace DungeonWorldBot.Rules;

/// <summary>
/// The Necromancer's Hexed Canopic Jars and the corpses kept in them.
/// </summary>
public static class JarRules
{
    public const string JarsName = "Hexed Canopic Jars";
    public const string Empty = "Empty";
    public const string CorpseCollector = "Corpse Collector";
    public const string RaiseTheDead = "Raise the Dead";
    public const string TrueCollector = "True Collector";
    public const int BaseJars = 3;

    /// <summary>
    /// The Power a corpse rises with on a hit.
    /// </summary>
    public const int RaisedPower = 3;

    public static ClassResource Power => Necromancer.Sheet.FindResource("Power")!;

    public static bool CanCollect(Character character) => ClassSheetLibrary.Knows(character, CorpseCollector);

    /// <summary>
    /// How many jars the character has. True Collector adds a special one.
    /// </summary>
    public static int Capacity(Character character) => BaseJars + (ClassSheetLibrary.Knows(character, TrueCollector) ? 1 : 0);

    public static HexedJars? Jars(Character character) =>
        character.Inventory?.Items.OfType<HexedJars>().FirstOrDefault();

    public static bool IsEmpty(string content) => content.Equals(Empty, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Corpses(Character character) =>
        Jars(character)?.Contents.Where(c => !IsEmpty(c)).ToList() ?? [];

    public static string? Find(Character character, string corpse) =>
        Corpses(character).FirstOrDefault(c => c.Equals(corpse.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every jar in order, each holding a corpse or "Empty", e.g. ["A Skinless Hound", "Empty", "Empty"].
    /// </summary>
    public static IReadOnlyList<string> Slots(Character character)
    {
        var corpses = Corpses(character);
        var empty = Math.Max(0, Capacity(character) - corpses.Count);
        return corpses.Concat(Enumerable.Repeat(Empty, empty)).ToList();
    }

    /// <summary>
    /// Why the character can't store another corpse, or null if they can.
    /// </summary>
    public static string? WhyCannotStore(Character character, string corpse)
    {
        if (IsEmpty(corpse.Trim()))
            return "Name the corpse, e.g. A Skinless Hound.";

        var held = Corpses(character).Count;
        return held >= Capacity(character)
            ? $"All {held} of {character.Name}'s jars are full. Open one with /jar open first."
            : null;
    }

    /// <summary>
    /// One line per jar for the character sheet, e.g. "🏺 A Skinless Hound".
    /// </summary>
    public static string Describe(Character character) =>
        string.Join('\n', Slots(character).Select(s => IsEmpty(s) ? "⚱️ *Empty*" : $"🏺 {s}"));
}
