using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Classes;

/// <summary>
/// The Battlemaster's active Tactic. Its effects are applied to their moves and damage automatically.
/// </summary>
[Group("tactic")]
public class TacticCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IAbilityService abilityService,
    IResourceService resourceService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    [Command("show")]
    [Description("See which Tactic you're in")]
    public async Task<IResult> ShowAsync(
        [Description("The user to see the Tactic of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await characterService.GetCharacterFromUserAsync(user.ID)
            : await characterService.GetCharacterFromUserAsync(GetUserId());
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        if (!TacticRules.UsesTactics(character))
            return await ReplyWithErrorAsync($"{character.Name} doesn't use Tactics.");

        return await ReplyAsync($"{character.Name}'s Tactic", TacticRules.Status(character), Fields(character));
    }

    [Command("change")]
    [Description("Switch to a new Tactic, after Change Tactics or another move lets you")]
    public async Task<IResult> ChangeAsync(
        [Description("The Tactic to switch to")]
        [AutocompleteProvider(TacticAutocomplete.ID)]
        string tactic)
    {
        var (character, error) = await GetTacticianAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (TacticRules.Find(tactic) is not { } chosen)
            return await ReplyWithErrorAsync($"{tactic} isn't a Tactic. Pick one of {string.Join(", ", TacticRules.All.Select(t => t.Name))}.");

        if (TacticRules.Current(character) == chosen)
            return await ReplyWithErrorAsync($"{character.Name} is already in {chosen.Name} Tactics.");

        await SetActiveAsync(character, chosen);

        return await ReplyAsync($"⚔️ {character.Name} switches to {chosen.Name} Tactics", TacticRules.Status(character), Fields(character));
    }

    [Command("default")]
    [Description("Spend 1 Gambit to return to your Default Tactic")]
    public async Task<IResult> ReturnToDefaultAsync(
        [Description("Return without spending a Gambit, e.g. when the GM says so")]
        bool free = false)
    {
        var (character, error) = await GetTacticianAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (TacticRules.Default(character) is not { } fallback)
            return await ReplyWithErrorAsync($"{character.Name} hasn't picked a Default Tactic. Pick one with /class learn");

        if (TacticRules.Current(character) == fallback)
            return await ReplyWithErrorAsync($"{character.Name} is already in their Default Tactic, {fallback.Name}.");

        var spent = string.Empty;
        if (!free)
        {
            var gambits = resourceService.GetValue(character, TacticRules.Gambit);
            if (gambits < 1)
                return await ReplyWithErrorAsync($"{character.Name} has no Gambits to spend. Use free to return anyway if the GM allows it.");

            await resourceService.SetValueAsync(character, TacticRules.Gambit, gambits - 1);
            spent = $"\n\nSpent 1 Gambit · {resourceService.Format(character, TacticRules.Gambit)} left";
        }

        await SetActiveAsync(character, fallback);

        return await ReplyAsync($"⚔️ {character.Name} falls back to {fallback.Name} Tactics", TacticRules.Status(character) + spent, Fields(character));
    }

    private static IReadOnlyList<IEmbedField> Fields(Character character) =>
        TacticRules.Reminders(character).ToList() is { Count: > 0 } reminders
            ? [new EmbedField("Also in this Tactic", string.Join('\n', reminders))]
            : [];

    /// <summary>
    /// Stores the active Tactic, or clears it when going back to the default so a later Default Tactic change carries over.
    /// </summary>
    private async Task SetActiveAsync(Character character, Tactic tactic)
    {
        var ability = abilityService.FindAbility(character, TacticRules.ActiveTactic);
        if (tactic == TacticRules.Default(character))
        {
            if (ability is not null)
                await abilityService.RemoveAbilityAsync(character, ability);
        }
        else if (ability is null)
        {
            await abilityService.AddAbilityAsync(character, TacticRules.ActiveTactic, tactic.Name, null);
        }
        else
        {
            await abilityService.SetDescriptionAsync(ability, tactic.Name);
        }
    }

    private async Task<(Character? Character, string? Error)> GetTacticianAsync()
    {
        var character = await characterService.GetCharacterFromUserAsync(GetUserId());
        if (character is null)
            return (null, NoCharacterMessage);

        if (!TacticRules.UsesTactics(character))
            return (null, $"{character.Name} doesn't use Tactics.");

        return (character, null);
    }
}

/// <summary>
/// The Battlemaster's Tactics, with what each does.
/// </summary>
public class TacticAutocomplete : IAutocompleteProvider
{
    public const string ID = "tactic";

    public string Identity => ID;

    public ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();
        IReadOnlyList<IApplicationCommandOptionChoice> choices = TacticRules.All
            .Where(t => t.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Select(t => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(Truncate($"{t.Name} · {t.Effect}", 100), t.Name))
            .ToList();
        return ValueTask.FromResult(choices);
    }

    // Discord rejects choice names over 100 characters
    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";
}
