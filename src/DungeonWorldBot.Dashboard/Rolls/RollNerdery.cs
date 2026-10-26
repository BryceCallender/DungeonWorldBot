namespace DungeonWorldBot.Dashboard.Rolls;

/// <summary>
/// The roll stats for the curious: how each player's luck has gone session by session, how each of them does with each
/// move, and who stood out last game night.
/// </summary>
public record RollNerdery(IReadOnlyList<LuckTrend> Luck, MoveHeatmap Heatmap, SessionAwards? LastSession);

/// <param name="Sessions">Their average dice (before modifiers) each session, oldest first, null where they didn't roll.</param>
public record LuckTrend(ulong UserID, IReadOnlyList<double?> Sessions);

/// <param name="Players">The columns, by Discord ID, most rolls first.</param>
/// <param name="Rows">The most rolled moves, most first.</param>
public record MoveHeatmap(IReadOnlyList<ulong> Players, IReadOnlyList<HeatmapRow> Rows);

/// <param name="Cells">How each player in <see cref="MoveHeatmap.Players"/> did with it, null if they never rolled it.</param>
public record HeatmapRow(string Move, int Rolls, IReadOnlyList<OutcomeTally?> Cells);

/// <param name="Name">e.g. "Session 12: Into the Sunken Abbey", when it was marked with /session start.</param>
public record SessionAwards(DateTime Started, IReadOnlyList<Award> Awards, string? Name = null);

/// <param name="Detail">Why they won it, e.g. "averaged 8.4 over 9 rolls".</param>
public record Award(string Icon, string Title, ulong UserID, string Detail);
