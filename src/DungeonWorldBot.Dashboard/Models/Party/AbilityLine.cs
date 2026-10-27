using DungeonWorldBot.Features.Spells;

namespace DungeonWorldBot.Dashboard.Models.Party;

/// <param name="GrantsSpells">The spell list this move lets the character cast from, like God Amidst the Wastes.</param>
public record AbilityLine(string Name, string? Description, int Uses, int? MaxUses, SpellList? GrantsSpells = null);
