using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes.DungeonWorld;
using DungeonWorldBot.Features.Classes.GrimWorld;
using DungeonWorldBot.Features.Classes.InverseWorld;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Features.Classes;

/// <summary>
/// Every class with sheet data. Move text is summarised from the published class sheets (Dungeon World's from the SRD
/// by Sage LaTorra and Adam Koebel, CC BY 3.0); players should keep their own sheets for the full wording.
/// </summary>
public static class ClassSheetLibrary
{
    public static readonly IReadOnlyList<ClassSheet> All =
    [
        Ranger.Sheet,
        Paladin.Sheet,
        Druid.Sheet,
        Barbarian.Sheet,
        Bard.Sheet,
        Cleric.Sheet,
        Fighter.Sheet,
        Immolator.Sheet,
        Thief.Sheet,
        Wizard.Sheet,

        Battlemaster.Sheet,
        Channeler.Sheet,
        Necromancer.Sheet,
        Slayer.Sheet,
        Templar.Sheet,
        Shaman.Sheet,

        Captain.Sheet,
        Collector.Sheet,
        Lantern.Sheet,
        Mechanic.Sheet,
        SkyDancer.Sheet,
        Survivor.Sheet,
        Walker.Sheet,
        Golem.Sheet,
    ];

    /// <returns>The sheet for a class, or null if the class doesn't have one yet.</returns>
    public static ClassSheet? For(ClassType type) => All.FirstOrDefault(s => s.Type == type);

    /// <summary>
    /// Resources every character has, whatever their class, like Bolster's Preparation.
    /// </summary>
    public static IReadOnlyList<ClassResource> CommonResources => [SpecialMoveRules.Preparation];

    /// <summary>
    /// The resources the character has right now: their class's (for the moves they know), then everyone's.
    /// </summary>
    public static IReadOnlyList<ClassResource> ResourcesOf(Character character) =>
        (For(character.Class.Type)?.ResourcesFor(KnownMoves(character)) ?? []).Concat(CommonResources).ToList();

    /// <summary>
    /// The names of every move and ability the character has.
    /// </summary>
    public static IReadOnlyCollection<string> KnownMoves(Character character) =>
        character.Abilities?.Select(a => a.Name).ToList() ?? [];

    /// <summary>
    /// Which of the six stats a class stat (like Lore) stands for. It's set by the first option the character picked
    /// for the deciding choice; options picked later don't change it.
    /// </summary>
    public static StatType? ResolveClassStat(Character character, ClassSheet sheet)
    {
        if (sheet.ClassStat is not { } classStat || sheet.Features.FirstOrDefault(f => f.Name == classStat.Feature) is not { } feature)
            return null;

        return character.Abilities?
            .OrderBy(a => a.ID)
            .Select(a => feature.Options.FirstOrDefault(o => o.Name.Equals(a.Name, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(o => o?.SetsClassStat is not null)?
            .SetsClassStat;
    }
}
