namespace DungeonWorldBot.Dashboard.Models.Scenes;

/// <summary>
/// The part of a map scene's board worth showing, in the board's own units.
/// </summary>
public record MapFrame(double X, double Y, double Width, double Height);
