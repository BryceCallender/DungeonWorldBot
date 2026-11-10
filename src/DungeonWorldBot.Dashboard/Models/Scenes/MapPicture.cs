namespace DungeonWorldBot.Dashboard.Models.Scenes;

/// <summary>
/// A picture on a map scene's board, like the map itself, where it sits and how big, in the board's own units.
/// </summary>
/// <param name="Src">Its address, one of the scene's own uploads.</param>
public record MapPicture(string Src, double X, double Y, double Width, double Height);
