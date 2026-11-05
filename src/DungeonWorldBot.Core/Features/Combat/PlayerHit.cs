using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Combat;

/// <summary>
/// A hit a player's character took, from the DM or an enemy.
/// </summary>
/// <param name="Reckless">The extra damage their Reckless Tactics added, explained, if any.</param>
public record PlayerHit(DamageTaken Taken, string? Reckless)
{
    /// <summary>
    /// e.g. "Aria took 3 damage! (14/20 HP)" with the armor's share, any extra damage and broken armor under it.
    /// </summary>
    public string Describe(Character character)
    {
        var description = $"{character.Name} took {Taken.Taken} damage! ({character.Health.CurrentHP}/{character.Health.MaxHP} HP)" +
                          (character.Health.CurrentHP == 0 ? " · at 0 HP, roll /move last-breath" : string.Empty) +
                          $"\n{DamageRules.Describe(Taken)}" +
                          (Reckless is not null ? $"\n*{Reckless}*" : string.Empty);
        if (Taken.Broken.Count > 0)
            description += $"\n*Broken: {string.Join(", ", Taken.Broken.Select(b => b.Source))}*";
        return description;
    }
}
