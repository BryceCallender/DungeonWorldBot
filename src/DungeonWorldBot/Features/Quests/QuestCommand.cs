using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Quests;

/// <summary>
/// A boon as the player typed it, with the detail for its blank.
/// </summary>
public record TypedBoon(string? Name, string? Detail);

/// <summary>
/// The boons typed by the player, or why they can't be had.
/// </summary>
public record BoonPicks(IReadOnlyList<QuestBoonChoice> Boons, string? Error = null);

/// <summary>
/// The Paladin's Quest: declaring a mission with its boons, recording the GM's vows, and ending it.
/// </summary>
[Group("quest")]
public class QuestCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IQuestService questService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string BoonDescription = "A boon for the quest";
    private const string DetailDescription = "Fills the boon's blank, e.g. fire for Invulnerability";

    [Command("declare")]
    [Description("Dedicate yourself to a mission through prayer and ritual cleansing, choosing its boons")]
    public async Task<IResult> DeclareAsync(
        [Description("The mission, e.g. Slay the Wyrm of Ashfall")]
        string mission,
        [Description(BoonDescription)] [AutocompleteProvider(QuestBoonAutocomplete.ID)]
        string? boon = null,
        [Description(DetailDescription)]
        string? detail = null,
        [Description(BoonDescription)] [AutocompleteProvider(QuestBoonAutocomplete.ID)] [Option("second-boon")]
        string? secondBoon = null,
        [Description(DetailDescription)] [Option("second-detail")]
        string? secondDetail = null,
        [Description("A third boon, with Perfect Knight")] [AutocompleteProvider(QuestBoonAutocomplete.ID)] [Option("third-boon")]
        string? thirdBoon = null,
        [Description(DetailDescription)] [Option("third-detail")]
        string? thirdDetail = null)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotQuest(character) is { } cannotQuest)
            return await ReplyWithErrorAsync(cannotQuest);

        if (QuestRules.Mission(character!) is { } current)
            return await ReplyWithErrorAsync($"{character!.Name} is already on a quest: {current}. End it with /quest end first.");

        var picks = PickBoons(character!, new(boon, detail), new(secondBoon, secondDetail), new(thirdBoon, thirdDetail));
        if (picks.Error is not null)
            return await ReplyWithErrorAsync(picks.Error);

        await questService.DeclareAsync(character!, mission, picks.Boons);

        var description = $"*{character!.Name} kneels in prayer and rises cleansed...*\n\n{QuestRules.Describe(character)}";
        if (QuestRules.QuestArmor(character) is > 0 and var armor)
            description += $"\n\n🛡️ +{armor} armor while on the quest.";
        if (QuestRules.SmiteDamage(character) is { } smite)
            description += $"\n⚔️ +{smite} damage while on the quest.";

        return await ReplyAsync("⚜️ A Quest Is Declared", description);
    }

    [Command("vow")]
    [Description("Record a vow the GM has set for your quest")]
    public async Task<IResult> VowAsync(
        [Description("The vow")] [AutocompleteProvider(QuestVowAutocomplete.ID)]
        string vow)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotQuest(character) is { } cannotQuest)
            return await ReplyWithErrorAsync(cannotQuest);

        if (!QuestRules.IsOnQuest(character!))
            return await ReplyWithErrorAsync($"{character!.Name} isn't on a quest. Declare one with /quest declare");

        if (QuestRules.FindVow(vow) is not { } known)
            return await ReplyWithErrorAsync($"{vow.Trim()} isn't a vow. The vows are {string.Join(", ", QuestRules.AllVows)}.");

        if (QuestRules.VowsOf(character!).Any(v => v.Name == QuestRules.VowPrefix + known))
            return await ReplyWithErrorAsync($"{character!.Name} has already vowed {known}.");

        await questService.AddVowAsync(character!, known);

        return await ReplyAsync($"⚜️ {character!.Name} vows {known}", QuestRules.Describe(character));
    }

    [Command("end")]
    [Description("End your quest, fulfilled or abandoned, losing its boons and vows")]
    public async Task<IResult> EndAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotQuest(character) is { } cannotQuest)
            return await ReplyWithErrorAsync(cannotQuest);

        if (QuestRules.Mission(character!) is not { } mission)
            return await ReplyWithErrorAsync($"{character!.Name} isn't on a quest.");

        await questService.EndAsync(character!);

        return await ReplyAsync("⚜️ Quest Ended", $"{character!.Name}'s quest is over: {mission}\n\nIts boons and vows fade.");
    }

    [Command("show")]
    [Description("See a Paladin's quest")]
    public async Task<IResult> ShowAsync(
        [Description("The user to see the quest of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = await characterService.GetCharacterToShowAsync(TargetOf(user));

        if (character is null)
            return await ReplyNoCharacterAsync(user);

        return await ReplyAsync($"{character.Name}'s Quest", QuestRules.Describe(character));
    }

    private static BoonPicks PickBoons(Character character, params TypedBoon[] typed)
    {
        var named = typed.Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();
        if (named.Count > QuestRules.MaxBoons(character))
            return new BoonPicks([], $"{character.Name} can choose up to {QuestRules.MaxBoons(character)} boons.");

        List<QuestBoonChoice> boons = [];
        foreach (var pick in named)
        {
            if (QuestRules.FindBoon(pick.Name!) is not { } boon)
                return new BoonPicks([], $"{pick.Name!.Trim()} isn't a boon. Try one of: {string.Join(", ", QuestRules.AllBoons.Select(b => b.Name))}");
            if (boons.Any(b => b.Boon == boon))
                return new BoonPicks([], $"{boon.Name} is in there twice.");
            if (boon.Blank is not null && string.IsNullOrWhiteSpace(pick.Detail))
                return new BoonPicks([], $"{boon.Name} needs a detail: {boon.Blank}.");
            boons.Add(new QuestBoonChoice(boon, pick.Detail));
        }

        return new BoonPicks(boons);
    }

    private static string? WhyCannotQuest(Character? character)
    {
        if (character is null)
            return NoCharacterMessage;
        return QuestRules.QuestAbility(character) is null ? $"{character.Name} doesn't know {QuestRules.Quest}." : null;
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());
}
