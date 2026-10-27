namespace DungeonWorldBot.Dashboard.Models.Scenes;

public record JoinResult(CanvasPerson Me, IReadOnlyList<CanvasPerson> People, IReadOnlyList<SavedItem> Items);
