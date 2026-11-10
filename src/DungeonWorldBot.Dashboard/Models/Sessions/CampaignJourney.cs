using DungeonWorldBot.Dashboard.Models.Scenes;

namespace DungeonWorldBot.Dashboard.Models.Sessions;

/// <summary>
/// The road the party has taken over the whole campaign, a session after another, on the campaign's map.
/// </summary>
/// <param name="Sessions">Oldest first, only those with somewhere to go.</param>
public record CampaignJourney(CampaignMapView Map, IReadOnlyList<Journey> Sessions)
{
    public IEnumerable<JourneyStop> Stops => Sessions.SelectMany(j => j.Stops);

    /// <summary>
    /// Places the party went that aren't on the map yet, once each.
    /// </summary>
    public IEnumerable<string> Unplaced =>
        Stops.Select(s => s.Place).Where(p => !Map.Places.ContainsKey(p)).Distinct(StringComparer.OrdinalIgnoreCase);
}
