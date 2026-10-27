namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Table">Everyone's rolls together, besides whoever was left out.</param>
/// <param name="Players">Each person's own spread, by Discord ID.</param>
public record RollSpreads(DiceSpread Table, IReadOnlyDictionary<ulong, DiceSpread> Players)
{
    public static readonly RollSpreads None = new(DiceSpread.Of([]), new Dictionary<ulong, DiceSpread>());
}
