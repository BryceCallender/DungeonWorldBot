using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Abilities;

[Scoped<IAbilityService>]
public class AbilityService(DungeonWorldContext dungeonWorldContext) : IAbilityService
{
    public Ability? FindAbility(Character character, string name)
    {
        return character.Abilities.FirstOrDefault(a => a.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Ability> AddAbilityAsync(Character character, string name, string? description, int? maxUses) =>
        (await AddAbilitiesAsync(character, [new NewAbility(name, description, maxUses)]))[0];

    public async Task<IReadOnlyList<Ability>> AddAbilitiesAsync(Character character, IEnumerable<NewAbility> abilities)
    {
        var added = abilities.Select(a => new Ability
        {
            Name = a.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(a.Description) ? null : a.Description.Trim(),
            MaxUses = a.MaxUses,
            Uses = a.MaxUses ?? 0,
            LevelTaken = character.Level,
        }).ToList();

        character.Abilities.AddRange(added);

        await dungeonWorldContext.SaveChangesAsync();
        return added;
    }

    public Task RemoveAbilityAsync(Character character, Ability ability) => RemoveAbilitiesAsync(character, [ability]);

    public async Task RemoveAbilitiesAsync(Character character, IEnumerable<Ability> abilities)
    {
        foreach (var ability in abilities.Distinct().ToList())
        {
            character.Abilities.Remove(ability);
            dungeonWorldContext.Remove(ability);
        }

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<bool> UseAbilityAsync(Ability ability, int amount)
    {
        if (!ability.IsLimited)
            return true;

        if (ability.Uses < amount)
            return false;

        ability.Uses -= amount;
        await dungeonWorldContext.SaveChangesAsync();
        return true;
    }

    public async Task RestoreAbilityAsync(Ability ability, int? amount = null)
    {
        if (ability.MaxUses is not { } max)
            return;

        // Restoring never takes away uses that were set above the max
        ability.Uses = amount is null
            ? Math.Max(max, ability.Uses)
            : Math.Max(ability.Uses, Math.Min(max, ability.Uses + amount.Value));
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Ability>> RestoreAllAbilitiesAsync(Character character)
    {
        var sheet = ClassSheetLibrary.For(character.Class.Type);
        List<Ability> kept = [];
        foreach (var ability in character.Abilities)
        {
            if (ability.MaxUses is not { } max)
                continue;

            if (sheet?.FindMove(ability.Name)?.RestoresWithAll == false)
                kept.Add(ability);
            else
                ability.Uses = Math.Max(max, ability.Uses);
        }

        await dungeonWorldContext.SaveChangesAsync();
        return kept;
    }

    public async Task SetAbilityUsesAsync(Ability ability, int uses)
    {
        // Not clamped to the max: hold can stack above the usual amount, and restoring brings it back to the max
        ability.Uses = uses;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task SetLevelTakenAsync(Ability ability, int level)
    {
        ability.LevelTaken = level;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task SetDescriptionAsync(Ability ability, string? description)
    {
        ability.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await dungeonWorldContext.SaveChangesAsync();
    }
}
