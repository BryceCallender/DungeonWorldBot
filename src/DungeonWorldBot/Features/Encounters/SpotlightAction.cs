namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// One thing someone did in a fight.
/// </summary>
/// <param name="Roll">What it rolled, e.g. "9 · Weak Hit (7-9)" or "d8+1 = 6".</param>
/// <param name="Target">Who it was aimed at, if anyone.</param>
/// <param name="Result">What happened to the target, e.g. "took 4".</param>
public record SpotlightAction(DateTimeOffset At, string Action, string? Roll, string? Target, string? Result, bool Missed)
{
    /// <summary>
    /// e.g. "Hack and Slash · 9 · Weak Hit (7-9) → Goblin 2 · took 4".
    /// </summary>
    public string Summary =>
        Action
        + (Roll is not null ? $" · {Roll}" : string.Empty)
        + (Target is not null ? $" → {Target}" : string.Empty)
        + (Result is not null ? $" · {Result}" : string.Empty);
}
