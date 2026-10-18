namespace DungeonWorldBot.Dashboard.Rolls;

/// <param name="Unlabelled">Rolls that weren't for a known move, like plain dice or ".dex dodge the trap".</param>
public sealed record RollBreakdown(
    IReadOnlyList<OutcomeTally> Moves,
    IReadOnlyList<OutcomeTally> Stats,
    IReadOnlyList<SessionRolls> Sessions,
    int Unlabelled);
