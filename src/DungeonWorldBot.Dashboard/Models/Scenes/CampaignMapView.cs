using DungeonWorldBot.Dashboard.Services.Encounters;

namespace DungeonWorldBot.Dashboard.Models.Scenes;

/// <summary>
/// The campaign's map: its pictures, and the places pinned on it by name.
/// </summary>
/// <param name="SceneID">The map scene's short ID, or null when there isn't one yet.</param>
/// <param name="Frame">Around its pictures and places, or where boards start when it has neither.</param>
public record CampaignMapView(string? SceneID, MapFrame Frame, IReadOnlyList<MapPicture> Pictures, IReadOnlyDictionary<string, BoardSpot> Places)
{
    public string? Url => SceneID is { } id ? $"scene/{id}" : null;
}
