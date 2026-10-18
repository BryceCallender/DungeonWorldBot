using DungeonWorldBot.Features.Spells;

namespace DungeonWorldBot.Dashboard.Party;

public sealed record SpellBook(SpellList List, string GrantedBy, int CasterLevel, int LevelsUsed, IReadOnlyList<Spell> Spells)
{
    public int LevelsAllowed => CasterLevel + 1;

    public bool OverLimit => LevelsUsed > LevelsAllowed;
}
