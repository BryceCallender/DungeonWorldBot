using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Services;

public interface IAbilityService
{
    Ability? FindAbility(Character character, string name);

    Task<Ability> AddAbilityAsync(Character character, string name, string? description, int? maxUses);

    Task RemoveAbilityAsync(Character character, Ability ability);

    /// <returns>False if the ability doesn't have enough uses left.</returns>
    Task<bool> UseAbilityAsync(Ability ability, int amount);

    /// <summary>
    /// Gives back uses, up to the ability's max. Restores fully when no amount is given.
    /// </summary>
    Task RestoreAbilityAsync(Ability ability, int? amount = null);

    Task RestoreAllAbilitiesAsync(Character character);

    Task SetAbilityUsesAsync(Ability ability, int uses);
}
