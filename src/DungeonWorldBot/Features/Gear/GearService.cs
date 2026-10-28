using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Gear;

[Scoped<IGearService>]
public class GearService(
    IAbilityService abilityService,
    IResourceService resourceService,
    IDiceRoller diceRoller)
    : IGearService
{
    public async Task GiveAsync(Character character, CustomGear gear)
    {
        if (abilityService.FindAbility(character, gear.Name) is null)
            await abilityService.AddAbilityAsync(character, gear.Name, GearRules.Describe(gear, null), null);
    }

    public async Task RemoveAsync(Character character, CustomGear gear)
    {
        foreach (var counter in gear.Modes.Select(m => m.Counter).OfType<GearCounter>())
            await SetCounterAsync(character, counter, 0);

        if (abilityService.FindAbility(character, gear.Name) is { } ability)
            await abilityService.RemoveAbilityAsync(character, ability);
    }

    public async Task SetModeAsync(Character character, CustomGear gear, GearMode mode)
    {
        var ability = abilityService.FindAbility(character, gear.Name)
                      ?? await abilityService.AddAbilityAsync(character, gear.Name, GearRules.Describe(gear, null), null);

        var previous = GearRules.Owned(character).FirstOrDefault(o => o.Gear == gear)?.Mode;
        if (previous?.Counter is { } oldCounter && previous != mode)
            await SetCounterAsync(character, oldCounter, 0);

        await abilityService.SetDescriptionAsync(ability, GearRules.Describe(gear, mode));
    }

    public int GetCounter(Character character, GearCounter counter) => resourceService.GetValue(character, counter.Resource);

    public Task<int> SetCounterAsync(Character character, GearCounter counter, int value) =>
        resourceService.SetValueAsync(character, counter.Resource, value);

    public async Task<GearDamage> ApplyToDamageAsync(Character character, string move, IReadOnlyList<(int Sides, int Value)> dice)
    {
        var bonus = 0;
        List<string> notes = [];

        foreach (var (gear, mode) in GearRules.Owned(character))
        {
            if (mode is null || !gear.AppliesTo(move))
                continue;

            var label = $"{mode.Name} ({gear.Name})";

            if (mode.RerollLowestDamageDie && GearRules.RerollLowest(dice, sides => diceRoller.Roll($"1d{sides}").Value) is { } reroll)
            {
                bonus += reroll.Gain;
                notes.Add($"{label}: rerolled a d{reroll.Sides}, {reroll.Before} → {reroll.Rolled}" +
                          (reroll.Gain > 0 ? $", +{reroll.Gain} damage" : ", kept the original"));
            }

            if (mode.DamageNote is not null)
                notes.Add($"{label}: {mode.DamageNote}");

            if (mode.Counter is { } counter)
            {
                var value = mode.Counter.GainOnDamage
                    ? await SetCounterAsync(character, counter, GetCounter(character, counter) + 1)
                    : GetCounter(character, counter);
                var max = counter.Max is { } m ? $"/{m}" : string.Empty;
                notes.Add(counter.GainOnDamage
                    ? $"{label}: +1 {counter.Name} ({value}{max})"
                    : $"{label}: {counter.Name} {value}{max}. Track them with /gear counter");
            }
        }

        return notes.Count == 0 ? GearDamage.None : new GearDamage(bonus, notes);
    }
}
