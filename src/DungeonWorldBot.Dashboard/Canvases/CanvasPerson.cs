namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// Someone on a canvas.
/// </summary>
/// <param name="ID">Their Discord ID as text, since JavaScript numbers can't hold a Discord ID exactly.</param>
public sealed record CanvasPerson(string ID, string Name, string? AvatarUrl, bool IsDungeonMaster);
