using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Abilities;

[Group("ability")]
public class AbilityCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IAbilityService abilityService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string NameDescription = "The name of the ability";

    // Discord rejects embeds with more than 25 fields
    private const int MaxFields = 25;

    [Command("add")]
    [Description("Add a class move, spell, or feature to your character")]
    public async Task<IResult> AddAbilityAsync(
        [Description(NameDescription)]
        string name,
        [Description("What the ability does")]
        string? description = null,
        [Description("Uses or hold it has when fully restored. Leave blank if it isn't limited")]
        [MinValue(1)]
        int? uses = null)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        if (string.IsNullOrWhiteSpace(name))
            return await ReplyWithErrorAsync("An ability needs a name.");

        if (abilityService.FindAbility(character, name) is not null)
            return await ReplyWithErrorAsync($"{character.Name} already has {name.Trim()}.");

        var ability = await abilityService.AddAbilityAsync(character, name, description, uses);

        return await ReplyAsync("Ability Added!", $"{character.Name} gained **{ability.ToDisplay()}**");
    }

    [Command("remove")]
    [Description("Remove an ability from your character")]
    public async Task<IResult> RemoveAbilityAsync([Description(NameDescription)] string name)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var ability = abilityService.FindAbility(character, name);
        if (ability is null)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have {name}.");

        await abilityService.RemoveAbilityAsync(character, ability);

        return await ReplyAsync("Ability Removed", $"{character.Name} no longer has **{ability.Name}**");
    }

    [Command("list")]
    [Description("Show a character's abilities")]
    public async Task<IResult> ListAbilitiesAsync(
        [Description("The user to see the abilities of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? NoCharacterMessage
                : $"{user.Username} does not have a character.");
        }

        var abilities = character.Abilities?.OrderBy(a => a.Name).ToList() ?? [];
        if (abilities.Count == 0)
            return await ReplyAsync($"{character.Name}'s Abilities", "No abilities yet. Add one with /ability add");

        var fields = abilities
            .Take(MaxFields)
            .Select(a => (IEmbedField)new EmbedField(a.ToDisplay(), Truncate(Describe(a), 1024)))
            .ToList();

        var description = abilities.Count > MaxFields
            ? $"Showing {MaxFields} of {abilities.Count} abilities"
            : $"{character.Class.Type}: Level {character.Level}";

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}'s Abilities",
                Description: description,
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("use")]
    [Description("Use an ability, spending uses or hold if it has them")]
    public async Task<IResult> UseAbilityAsync(
        [Description(NameDescription)]
        string name,
        [Description("How many uses or hold to spend")]
        [MinValue(1)]
        int amount = 1)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var ability = abilityService.FindAbility(character, name);
        if (ability is null)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have {name}.");

        if (!await abilityService.UseAbilityAsync(ability, amount))
            return await ReplyWithErrorAsync($"{ability.Name} only has {ability.Uses} left. Restore it with /ability restore");

        var used = ability.IsLimited
            ? $"{character.Name} used **{ability.Name}** ({ability.Uses}/{ability.MaxUses} left)"
            : $"{character.Name} used **{ability.Name}**";

        return await ReplyAsync(
            ability.Name,
            ability.Description is null ? used : $"{used}\n\n*{ability.Description}*");
    }

    [Command("restore")]
    [Description("Restore uses or hold, e.g. after making camp. Leave the name blank to restore everything")]
    public async Task<IResult> RestoreAbilityAsync(
        [Description("The ability to restore. Leave blank to restore all of them")]
        string? name = null,
        [Description("How many uses to restore. Leave blank to fully restore")]
        [MinValue(1)]
        int? amount = null)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        if (string.IsNullOrWhiteSpace(name))
        {
            await abilityService.RestoreAllAbilitiesAsync(character);
            return await ReplyAsync("Abilities Restored!", $"All of {character.Name}'s abilities were fully restored");
        }

        var ability = abilityService.FindAbility(character, name);
        if (ability is null)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have {name}.");
        if (!ability.IsLimited)
            return await ReplyWithErrorAsync($"{ability.Name} doesn't have limited uses to restore.");

        await abilityService.RestoreAbilityAsync(ability, amount);

        return await ReplyAsync("Ability Restored!", $"{character.Name}'s **{ability.ToDisplay()}**");
    }

    [Command("set-uses")]
    [Description("Set how many uses or hold an ability has right now, e.g. after rolling for hold")]
    public async Task<IResult> SetUsesAsync(
        [Description(NameDescription)]
        string name,
        [Description("The new number of uses or hold")]
        [MinValue(0)]
        int uses)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var ability = abilityService.FindAbility(character, name);
        if (ability is null)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have {name}.");
        if (!ability.IsLimited)
            return await ReplyWithErrorAsync($"{ability.Name} doesn't track uses. Remove it and add it again with uses to start tracking.");

        await abilityService.SetAbilityUsesAsync(ability, uses);

        return await ReplyAsync("Uses Changed!", $"{character.Name}'s **{ability.ToDisplay()}**");
    }

    [Command("set-level")]
    [Description("Set the level you took an ability at, e.g. Divine Favor, which your cleric level counts up from")]
    public async Task<IResult> SetLevelTakenAsync(
        [Description(NameDescription)]
        string name,
        [Description("Your character level when you took it")]
        [MinValue(1)]
        int level)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        var ability = abilityService.FindAbility(character, name);
        if (ability is null)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have {name}.");
        if (level > character.Level)
            return await ReplyWithErrorAsync($"{character.Name} is only level {character.Level}, so they can't have taken {ability.Name} at level {level}.");

        await abilityService.SetLevelTakenAsync(ability, level);

        return await ReplyAsync("Level Taken Set", $"{character.Name} took **{ability.Name}** at level {level}. " +
                                                   "Anything that counts up from it, like a cleric level from Divine Favor, follows from there.");
    }

    // The level is only known for abilities added since it was recorded
    private static string Describe(Ability ability) =>
        ability.LevelTaken is { } level ? $"{ability.Description ?? "—"}\n*Taken at level {level}*" : ability.Description ?? "—";

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());
}
