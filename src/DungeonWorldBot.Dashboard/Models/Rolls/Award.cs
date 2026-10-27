namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Detail">Why they won it, e.g. "averaged 8.4 over 9 rolls".</param>
public record Award(string Icon, string Title, ulong UserID, string Detail);
