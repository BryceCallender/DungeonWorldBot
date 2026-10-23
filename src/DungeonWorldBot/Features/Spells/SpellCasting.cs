using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Spells;

/// <summary>
/// A spell list the character can cast from, with the resource that tracks their caster level for it.
/// </summary>
public record CasterAccess(SpellList List, ClassResource LevelResource, string GrantedBy);

/// <summary>
/// The Commune / Prepare Spells rules: spells up to your caster level, with total levels up to caster level+1.
/// Rotes and cantrips are free.
/// </summary>
public static class SpellCasting
{
    /// <summary>
    /// Added to a learned spell's description, recording which list it's from (Light is on both).
    /// </summary>
    public static string ListNote(SpellList list) => $"*{list} spell.*";

    /// <summary>
    /// The spell lists the character can cast from, through moves on their own sheet or learned from another class.
    /// </summary>
    public static IReadOnlyList<CasterAccess> Access(Character character)
    {
        if (ClassSheetLibrary.For(character.Class.Type) is not { } own)
            return [];

        var known = ClassSheetLibrary.KnownMoves(character);
        var moves = own.AllMoves
            .Where(m => known.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
            .Select(m => (Sheet: own, Move: m))
            .Concat(Multiclassing.KnownForeignMoves(character, own).Select(f => (f.Sheet, f.Move)));

        return moves
            .Where(m => m.Move.GrantsSpells is not null)
            .Select(m => m.Sheet.FindResource(m.Move.GrantsSpells!.LevelResource) is { } resource
                ? new CasterAccess(m.Move.GrantsSpells.List, resource, m.Move.Name)
                : null)
            .OfType<CasterAccess>()
            .DistinctBy(a => a.List)
            .ToList();
    }

    /// <summary>
    /// The spells the character has learned.
    /// </summary>
    public static IReadOnlyList<Spell> KnownSpells(Character character) =>
        (character.Abilities ?? [])
            .Select(a => SpellLists.Find(a.Name).FirstOrDefault(s => a.Description?.Contains(ListNote(s.List)) == true))
            .OfType<Spell>()
            .ToList();

    /// <summary>
    /// Total levels of the character's spells from a list, not counting rotes and cantrips.
    /// </summary>
    public static int LevelsUsed(IEnumerable<Spell> known, SpellList list) =>
        known.Where(s => s.List == list && !s.IsFree).Sum(s => s.Level);

    /// <returns>Why the spell can't be learned at this caster level, or null if it can.</returns>
    public static string? WhyCannotLearn(Spell spell, int casterLevel, IReadOnlyList<Spell> known)
    {
        if (known.Contains(spell))
            return $"You already have {spell.Name}.";

        if (spell.IsFree)
            return null;

        if (spell.Level > casterLevel)
            return $"{spell.Name} is level {spell.Level}, above your {spell.List.ToString().ToLower()} level of {casterLevel}.";

        var used = LevelsUsed(known, spell.List);
        if (used + spell.Level > casterLevel + 1)
        {
            return $"You have {used} of {casterLevel + 1} spell levels in use, and {spell.Name} needs {spell.Level}. " +
                   "Remove one with /ability remove, or start over with /spell clear when you Commune or prepare spells.";
        }

        return null;
    }
}
