using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Debilities;

public interface IDebilityService
{
    /// <returns>False if the character already has it.</returns>
    Task<bool> AddAsync(Character character, DebilityType debility);

    /// <returns>False if the character didn't have it.</returns>
    Task<bool> RemoveAsync(Character character, DebilityType debility);

    /// <returns>The debilities removed.</returns>
    Task<IReadOnlyList<DebilityType>> ClearAsync(Character character);
}
