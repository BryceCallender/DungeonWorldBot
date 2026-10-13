using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Subclasses.Homebrew;

namespace DungeonWorldBot.Subclasses;

/// <summary>
/// Every subclass. Add a homebrew subclass by giving it a file in Homebrew/ and listing it here.
/// </summary>
public static class SubclassLibrary
{
    public static readonly IReadOnlyList<Subclass> All =
    [
        ChampionOfLuna.Subclass,
        TacticalGuardian.Subclass,
        CircleOfTheEclipse.Subclass,
        StormCaller.Subclass,
    ];

    public static Subclass? Find(string name) =>
        All.FirstOrDefault(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The subclass the character belongs to, if any.
    /// </summary>
    public static Subclass? Of(Character character)
    {
        var known = ClassSheetLibrary.KnownMoves(character);
        return All.FirstOrDefault(s => known.Contains(s.Name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every subclass bonus to a roll, from the roller's own subclass and from anyone else in the party whose subclass
    /// shares its power (like a Boon's Echo).
    /// </summary>
    /// <param name="party">Everyone in the campaign. The roller is counted whether or not they're in it.</param>
    public static IReadOnlyList<RollBonus> Bonuses(Character roller, IEnumerable<Character> party, RollKind kind, StatType? stat = null) =>
        party
            .Where(c => c.ID != roller.ID)
            .Prepend(roller)
            .SelectMany(owner => Of(owner)?.Bonuses?.Invoke(owner, roller, kind, stat) ?? [])
            .Where(b => b.Amount != 0)
            .ToList();
}
