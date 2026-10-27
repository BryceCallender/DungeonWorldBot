namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Players">The columns, by Discord ID, most rolls first.</param>
/// <param name="Rows">The most rolled moves, most first.</param>
public record MoveHeatmap(IReadOnlyList<ulong> Players, IReadOnlyList<HeatmapRow> Rows);
