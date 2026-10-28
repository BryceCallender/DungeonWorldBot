using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// The special moves for camping, travel and time in town.
/// </summary>
[Group("downtime")]
public class DowntimeCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService,
    ISpecialMoveService specialMoveService)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService)
{
    [Command("make-camp")]
    [Description("Settle in to rest: eat a ration, and heal half your max HP if you sleep a few hours")]
    public async Task<IResult> MakeCampAsync(
        [Description("Whether you got a few uninterrupted hours of sleep (default yes)")]
        bool slept = true)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        var camp = await specialMoveService.MakeCampAsync(character, slept);

        List<IEmbedField> fields =
        [
            new EmbedField("Ration", camp.Ration is null
                ? "You have no rations. The GM will say what going hungry costs you."
                : $"Ate 1 {camp.Ration}, {camp.RationsLeft} left."),
            new EmbedField("Sleep", slept
                ? $"Healed {camp.Healed} HP ({character.Health.CurrentHP}/{character.Health.MaxHP})."
                : "Your rest was interrupted, so you don't heal."),
        ];

        return await ReplyAsync($"🏕️ {character.Name}: Make Camp", "*If you're somewhere dangerous, decide the watch order. Whoever's on watch rolls /downtime take-watch when something approaches.*", fields);
    }

    [Command("take-watch")]
    [Description("You're on watch and something approaches the camp (+WIS)")]
    public Task<IResult> TakeWatchAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(SpecialMoves.TakeWatch, StatType.Wis, bonus, prepared: prepared);

    [Command("perilous-journey")]
    [Description("Do your job while the party travels through hostile territory (+WIS)")]
    public Task<IResult> PerilousJourneyAsync(
        [Description("Trailblazer: cut travel time, Scout: spot trouble, Quartermaster: save rations")]
        JourneyRole role,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(SpecialMoves.PerilousJourney(role), StatType.Wis, bonus, prepared: prepared);

    [Command("recover")]
    [Description("Rest in comfort and safety: heal all your HP, and clear debilities after enough days")]
    public async Task<IResult> RecoverAsync(
        [Description("How many days you rested")]
        [MinValue(1)]
        int days,
        [Description("Whether a healer (magical or otherwise) cared for you")]
        bool healer = false,
        [Description("The debility to clear first, if you've earned some but not all of them back")]
        DebilityType? debility = null)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        var recovery = await specialMoveService.RecoverAsync(character, days, healer, debility);

        List<IEmbedField> fields = [new EmbedField("HP", $"Healed {recovery.Healed} HP ({character.Health.CurrentHP}/{character.Health.MaxHP}).")];
        if (recovery.Removed.Count > 0)
            fields.Add(new EmbedField("Debilities Cleared", string.Join(", ", recovery.Removed.Select(DebilityRules.Describe))));
        if (recovery.StillToRemove > 0)
            fields.Add(new EmbedField("Debilities to Clear", $"You can clear {recovery.StillToRemove} more of your choice with /debility remove."));

        var pace = healer ? "every 2 days under a healer's care" : "every 3 days";
        return await ReplyAsync($"🛏️ {character.Name}: Recover", $"*{days} day{(days == 1 ? "" : "s")} of rest. One debility clears {pace}.*", fields);
    }

    [Command("bolster")]
    [Description("Spend your leisure time in study, meditation, or hard practice to take Preparation")]
    public async Task<IResult> BolsterAsync(
        [Description("Week: a week or more, for 1 Preparation. Month: a month or longer, for 3")]
        BolsterLength length)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        var preparation = SpecialMoveRules.Preparation;
        var gained = SpecialMoveRules.PreparationFor(length);
        var total = await _resourceService.SetValueAsync(character, preparation, _resourceService.GetValue(character, preparation) + gained);

        return await ReplyAsync(
            $"📚 {character.Name}: Bolster",
            $"*A {length.ToString().ToLower()} of preparing.* Took {gained} Preparation, {total} in all.\n\nWhen it pays off, add `prepared: True` to a move roll to spend 1 for +1. Only one per roll.");
    }

    [Command("supply")]
    [Description("Buy something special or beyond what's usually for sale here (+CHA)")]
    public Task<IResult> SupplyAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(SpecialMoves.Supply, StatType.Cha, bonus, prepared: prepared);

    [Command("carouse")]
    [Description("Throw a big party: spend 100 coin, +1 for each extra 100 you spend")]
    public async Task<IResult> CarouseAsync(
        [Description("How many extra 100s of coin to spend, for +1 each")]
        [MinValue(0)]
        int extra = 0,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        if (prepared && WhyCannotSpendPreparation(character) is { } unprepared)
            return await ReplyWithErrorAsync(unprepared);

        var cost = SpecialMoveRules.CarouseCost * (1 + extra);
        if (!await specialMoveService.SpendCoinsAsync(character, cost))
            return await ReplyWithErrorAsync($"Carousing like that costs {cost} coin, and you have {SpecialMoveRules.Coins(character)?.Amount ?? 0}.");

        return await ReplyWithMoveAsync(character, SpecialMoves.Carouse, $"Extra 100s ({cost} coin spent)", extra, bonus, prepared: prepared);
    }

    [Command("outstanding-warrants")]
    [Description("Return to a civilized place where you've caused trouble before (+CHA)")]
    public Task<IResult> OutstandingWarrantsAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(SpecialMoves.OutstandingWarrants, StatType.Cha, bonus, prepared: prepared);
}
