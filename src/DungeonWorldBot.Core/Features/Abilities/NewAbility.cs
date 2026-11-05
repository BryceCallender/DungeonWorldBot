using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Abilities;

/// <summary>
/// An ability to give a character, before it's saved.
/// </summary>
/// <param name="MaxUses">How many times it can be used before it's restored, if it's limited.</param>
public record NewAbility(string Name, string? Description, int? MaxUses = null)
{
    /// <summary>
    /// A class or subclass move, learned as it's written on the sheet.
    /// </summary>
    public static NewAbility From(ClassMove move) => new(move.Name, move.Summary, move.Uses);
}
