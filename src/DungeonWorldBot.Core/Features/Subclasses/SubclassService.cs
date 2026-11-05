using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Subclasses;

[Scoped<ISubclassService>]
public class SubclassService(IAbilityService abilityService) : ISubclassService
{
    public async Task<IReadOnlyList<ClassMove>> JoinAsync(Character character, Subclass subclass)
    {
        if (abilityService.FindAbility(character, subclass.Name) is null)
            await abilityService.AddAbilityAsync(character, subclass.Name, $"*{subclass.Title}.* {subclass.Summary}", null);

        return await GrantUnlockedMovesAsync(character);
    }

    public async Task LeaveAsync(Character character, Subclass subclass)
    {
        var names = subclass.Moves.Select(m => m.Move.Name).Concat(subclass.StateAbilities).Append(subclass.Name);
        await abilityService.RemoveAbilitiesAsync(character, names.Select(n => abilityService.FindAbility(character, n)).OfType<Ability>());
    }

    public string? WhyCantUse(Character character, Subclass subclass, string move)
    {
        if (SubclassLibrary.Of(character) != subclass)
            return $"{character.Name} isn't a {subclass.Name}. See /subclass info {subclass.Name}";
        if (abilityService.FindAbility(character, move) is not null)
            return null;

        var level = subclass.Moves.FirstOrDefault(m => m.Move.Name == move)?.Level;
        return $"{character.Name} hasn't unlocked {move} yet" + (level is not null ? $" (level {level})." : ".");
    }

    public async Task<IReadOnlyList<ClassMove>> GrantUnlockedMovesAsync(Character character)
    {
        if (SubclassLibrary.Of(character) is not { } subclass)
            return [];

        var learned = subclass.MovesAt(character.Level).Where(m => abilityService.FindAbility(character, m.Name) is null).ToList();
        await abilityService.AddAbilitiesAsync(character, learned.Select(NewAbility.From));
        return learned;
    }
}
