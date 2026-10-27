using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Dashboard.Models.Party;

/// <param name="IgnoredBy">The move that lets the character ignore the debility, like the Slayer's Tenacity.</param>
public record StatLine(StatType Type, int Value, int Modifier, DebilityType? Debility, string? IgnoredBy)
{
    public int RollModifier => Debility is not null && IgnoredBy is null ? Modifier + DebilityRules.Penalty : Modifier;
}
