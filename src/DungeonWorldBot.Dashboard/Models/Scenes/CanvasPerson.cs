namespace DungeonWorldBot.Dashboard.Models.Scenes;

/// <summary>
/// Someone on a canvas.
/// </summary>
/// <param name="ID">Their Discord ID as text, since JavaScript numbers can't hold a Discord ID exactly.</param>
public record CanvasPerson(string ID, string Name, string? AvatarUrl, bool IsDungeonMaster);
