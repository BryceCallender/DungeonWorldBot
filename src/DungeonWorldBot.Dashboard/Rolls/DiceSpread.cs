using DungeonWorldBot.Features.Rolls;

namespace DungeonWorldBot.Dashboard.Rolls;

/// <param name="Sums">How many times each natural 2d6 sum came up, indexed 0–10 for sums 2–12.</param>
public sealed record DiceSpread(int Rolls, double AverageNatural, double AverageTotal, IReadOnlyList<int> Sums)
{
    public static DiceSpread Of(IReadOnlyCollection<MoveDice> dice)
    {
        var sums = new int[11];
        foreach (var d in dice)
            sums[d.First + d.Second - 2]++;

        return new DiceSpread(
            dice.Count,
            dice.Count == 0 ? 0 : dice.Average(d => d.First + d.Second),
            dice.Count == 0 ? 0 : dice.Average(d => d.Total),
            sums);
    }

    /// <summary>
    /// The share of fair 2d6 rolls that land on each sum from 2 to 12.
    /// </summary>
    public static double Expected(int sum) => (6 - Math.Abs(sum - 7)) / 36.0;
}
