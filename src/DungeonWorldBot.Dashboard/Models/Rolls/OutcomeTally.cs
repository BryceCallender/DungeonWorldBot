using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;

namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <summary>
/// How a group of rolls went, like every Hack and Slash or every +DEX roll.
/// </summary>
public record OutcomeTally(string Name, int Rolls, int StrongHits, int WeakHits, int Misses)
{
    public static OutcomeTally Of(string name, IReadOnlyCollection<MoveDice> dice) => new(
        name,
        dice.Count,
        dice.Count(d => d.Outcome == MoveOutcome.StrongHit),
        dice.Count(d => d.Outcome == MoveOutcome.WeakHit),
        dice.Count(d => d.Outcome == MoveOutcome.Miss));
}
