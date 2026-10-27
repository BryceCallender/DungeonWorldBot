namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <summary>
/// The roll stats for the curious: how each player's luck has gone session by session, how each of them does with each
/// move, and who stood out last game night.
/// </summary>
public record RollNerdery(IReadOnlyList<LuckTrend> Luck, MoveHeatmap Heatmap, SessionAwards? LastSession);
