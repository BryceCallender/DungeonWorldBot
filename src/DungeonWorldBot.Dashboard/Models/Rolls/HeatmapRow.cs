namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Cells">How each player in <see cref="MoveHeatmap.Players"/> did with it, null if they never rolled it.</param>
public record HeatmapRow(string Move, int Rolls, IReadOnlyList<OutcomeTally?> Cells);
