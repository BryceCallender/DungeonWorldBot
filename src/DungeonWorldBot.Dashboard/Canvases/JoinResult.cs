namespace DungeonWorldBot.Dashboard.Canvases;

public record JoinResult(CanvasPerson Me, IReadOnlyList<CanvasPerson> People, IReadOnlyList<SavedItem> Items);
