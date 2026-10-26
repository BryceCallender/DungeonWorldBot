namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// One thing that happened in a fight, and who did it.
/// </summary>
/// <param name="Actor">Who acted, as they were called at the time.</param>
public record TimelineEntry(string Actor, SpotlightAction Action);
