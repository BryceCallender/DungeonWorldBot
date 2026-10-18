namespace DungeonWorldBot.Dashboard.Canvases;

public sealed record JoinResult(CanvasPerson Me, IReadOnlyList<CanvasPerson> People, IReadOnlyList<SavedItem> Items);
