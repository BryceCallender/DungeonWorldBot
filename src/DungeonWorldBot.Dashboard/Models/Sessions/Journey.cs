using DungeonWorldBot.Dashboard.Models.Scenes;

namespace DungeonWorldBot.Dashboard.Models.Sessions;

/// <summary>
/// The road the party took over a session, stop by stop, and the map to play it back on.
/// </summary>
public record Journey(SessionSummary Session, CampaignMapView Map, IReadOnlyList<JourneyStop> Stops);
