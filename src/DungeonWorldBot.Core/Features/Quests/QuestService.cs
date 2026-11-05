using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Quests;

/// <summary>
/// A boon picked for a quest, with the player's blank filled in.
/// </summary>
public record QuestBoonChoice(QuestBoon Boon, string? Detail);

public interface IQuestService
{
    /// <summary>
    /// Starts a quest: the mission, its boons, and any armor from Holy or Divine Protection.
    /// </summary>
    Task DeclareAsync(Character character, string mission, IReadOnlyList<QuestBoonChoice> boons);

    Task AddVowAsync(Character character, string vow);

    /// <summary>
    /// Ends the quest, taking away its boons, vows and armor.
    /// </summary>
    Task EndAsync(Character character);
}

[Scoped<IQuestService>]
public class QuestService(IAbilityService abilityService, IArmorService armorService) : IQuestService
{
    public async Task DeclareAsync(Character character, string mission, IReadOnlyList<QuestBoonChoice> boons)
    {
        await abilityService.SetDescriptionAsync(QuestRules.QuestAbility(character)!, QuestRules.MissionPrefix + mission.Trim());

        await abilityService.AddAbilitiesAsync(character,
            boons.Select(choice => new NewAbility(QuestRules.BoonPrefix + choice.Boon.Name, QuestRules.Describe(choice.Boon, choice.Detail))));

        if (QuestRules.QuestArmor(character) is > 0 and var armor)
            await armorService.AddBonusAsync(character, QuestRules.ArmorSource, ArmorType.Physical, armor, ArmorDuration.UntilRemoved);
    }

    public Task AddVowAsync(Character character, string vow) =>
        abilityService.AddAbilityAsync(character, QuestRules.VowPrefix + vow, $"The GM has set a vow of {vow.ToLower()} for this quest.", null);

    public async Task EndAsync(Character character)
    {
        await abilityService.RemoveAbilitiesAsync(character, QuestRules.BoonsOf(character).Concat(QuestRules.VowsOf(character)));

        await abilityService.SetDescriptionAsync(QuestRules.QuestAbility(character)!, QuestRules.QuestMove.Summary);
        await armorService.RemoveBonusesAsync(character, QuestRules.ArmorSource);
    }
}
