using DungeonWorldBot.Features.Spells;

namespace DungeonWorldBot.Dashboard.Models.Party;

public record SpellBook(SpellList List, string GrantedBy, int CasterLevel, int LevelsUsed, IReadOnlyList<Spell> Spells)
{
    public int LevelsAllowed => CasterLevel + 1;

    public bool OverLimit => LevelsUsed > LevelsAllowed;
}
