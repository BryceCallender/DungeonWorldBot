using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Abilities;

public interface IAbilityService
{
    Ability? FindAbility(Character character, string name);

    Task<Ability> AddAbilityAsync(Character character, string name, string? description, int? maxUses);

    /// <summary>
    /// Gives the character several abilities at once, saved together.
    /// </summary>
    /// <returns>The abilities, in the order given.</returns>
    Task<IReadOnlyList<Ability>> AddAbilitiesAsync(Character character, IEnumerable<NewAbility> abilities);

    Task RemoveAbilityAsync(Character character, Ability ability);

    /// <summary>
    /// Takes several abilities away at once, saved together.
    /// </summary>
    Task RemoveAbilitiesAsync(Character character, IEnumerable<Ability> abilities);

    /// <returns>False if the ability doesn't have enough uses left.</returns>
    Task<bool> UseAbilityAsync(Ability ability, int amount);

    /// <summary>
    /// Gives back uses, up to the ability's max. Restores fully when no amount is given.
    /// </summary>
    Task RestoreAbilityAsync(Ability ability, int? amount = null);

    /// <summary>
    /// Fully restores every limited ability, except those whose uses only come back another way (poison doses, totems).
    /// </summary>
    /// <returns>The limited abilities it left alone.</returns>
    Task<IReadOnlyList<Ability>> RestoreAllAbilitiesAsync(Character character);

    Task SetAbilityUsesAsync(Ability ability, int uses);

    Task SetDescriptionAsync(Ability ability, string? description);

    Task SetLevelTakenAsync(Ability ability, int level);
}
