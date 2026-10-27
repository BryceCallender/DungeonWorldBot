using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;

namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <summary>
/// Hits (or misses) in a row, counting back from someone's latest roll: on fire after three hits, frozen after three misses.
/// </summary>
/// <param name="Hot">Hits (7+) rather than misses.</param>
public record RollStreak(bool Hot, int Length)
{
    public const int Minimum = 3;

    public string Icon => Hot ? "🔥" : "🧊";

    public string Describe() => Hot ? $"{Length} hits in a row" : $"{Length} misses in a row";

    /// <param name="newestFirst">One person's rolls, latest first.</param>
    /// <returns>The streak they're on, or null if it's shorter than <see cref="Minimum"/>.</returns>
    public static RollStreak? Of(IEnumerable<RollRecord> newestFirst)
    {
        bool? hot = null;
        var length = 0;
        foreach (var roll in newestFirst)
        {
            var hit = MoveDice.Of(roll).Outcome != MoveOutcome.Miss;
            hot ??= hit;
            if (hit != hot)
                break;
            length++;
        }

        return hot is { } h && length >= Minimum ? new RollStreak(h, length) : null;
    }
}
