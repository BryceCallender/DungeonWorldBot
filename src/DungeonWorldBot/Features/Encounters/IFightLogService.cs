using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Encounters;

public interface IFightLogService
{
    /// <summary>
    /// What's been done in the fight. With several fights going, a roll is only this fight's if it was logged here, or,
    /// for a plain roll nobody logged, if the player is in this fight rather than another.
    /// </summary>
    Task<FightLog> GetAsync(Encounter encounter);
}
