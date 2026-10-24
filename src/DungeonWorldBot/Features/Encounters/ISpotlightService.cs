using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Encounters;

public interface ISpotlightService
{
    /// <summary>
    /// Who in the fight has acted, and who's been left out. Players' 2d6 rolls in game chat count as acting, along with
    /// the moves, NPC rolls and enemy attacks logged in the fight, which say who they were aimed at.
    /// </summary>
    Task<Spotlight> GetAsync(Encounter encounter, IEnumerable<Character> party);
}
