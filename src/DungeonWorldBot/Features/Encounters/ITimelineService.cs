using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Encounters;

public interface ITimelineService
{
    /// <summary>
    /// What's happened in the fight, newest first, a page at a time, including what was done by those who've since
    /// fallen or left.
    /// </summary>
    /// <param name="party">Everyone's characters, to name the players behind plain rolls.</param>
    /// <param name="before">Only what happened before this: the time of the oldest entry already shown.</param>
    Task<TimelinePage> GetAsync(Encounter encounter, IEnumerable<Character> party, int take, DateTimeOffset? before = null);
}
