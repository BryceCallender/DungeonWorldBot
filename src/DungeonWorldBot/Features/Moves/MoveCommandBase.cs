using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// What /move and /downtime share: rolling a move, spending Preparation on it, and recording the roll.
/// </summary>
public abstract class MoveCommandBase(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    protected const string BonusDescription = "Any +forward, ongoing bonus, or penalty to add to the roll";
    protected const string PreparedDescription = "Spend a Preparation from Bolster for +1";
    protected const string NoMoverMessage = "You must have a character to make moves. Try using /character create";

    protected readonly ICharacterService _characterService = characterService;
    protected readonly IResourceService _resourceService = resourceService;

    /// <param name="target">The enemy to take the damage off, for moves that roll it.</param>
    /// <param name="extraDamage">Flat damage added to the damage roll, for something the bot doesn't track.</param>
    /// <param name="prepared">Whether they're spending a Preparation for +1.</param>
    protected async Task<IResult> MakeStatMoveAsync(MoveDefinition move, StatType statType, int bonus, string? target = null, int extraDamage = 0, bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        return await ReplyWithMoveAsync(character, move, statType.ToString().ToUpper(), stat.Modifier, bonus, statType, target: target, extraDamage: extraDamage, prepared: prepared);
    }

    /// <param name="target">The enemy to take the damage off, if the move rolls damage and hits.</param>
    /// <param name="stat">The stat being rolled, so its debility (if any) applies.</param>
    /// <param name="kind">What the roll is for, when subclass bonuses (like a Boon of Luna) could apply to it.</param>
    /// <param name="extraDamage">Flat damage added to the damage roll, if the move rolls damage.</param>
    /// <param name="ally">The player's character being helped or hindered, for the spotlight.</param>
    /// <param name="prepared">Whether they're spending a Preparation for +1.</param>
    protected async Task<IResult> ReplyWithMoveAsync(
        Character character,
        MoveDefinition move,
        string? modifierLabel,
        int modifier,
        int bonus,
        StatType? stat = null,
        RollKind? kind = null,
        string? target = null,
        int extraDamage = 0,
        string? ally = null,
        bool prepared = false)
    {
        if (TacticRules.WhyCannotMake(character, move.Name) is { } forbidden)
            return await ReplyWithErrorAsync(forbidden);

        if (prepared)
        {
            if (WhyCannotSpendPreparation(character) is { } unprepared)
                return await ReplyWithErrorAsync(unprepared);

            var preparation = SpecialMoveRules.Preparation;
            await _resourceService.SetValueAsync(character, preparation, _resourceService.GetValue(character, preparation) - 1);
        }

        var resolution = await moveResolver.ResolveAsync(
            character,
            move,
            new MoveInput(modifierLabel, modifier, bonus, stat, kind, target, extraDamage, prepared));

        var reply = await _feedbackService.SendContextualEmbedAsync(resolution.Embed, ct: CancellationToken);

        if (!reply.IsSuccess)
            return reply;

        if (_context.TryGetChannelID(out var channelID))
            await rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, resolution.Roll.Dice, RollLabel.Of(resolution.Embed));

        // Does nothing between fights
        await encounterService.LogAsync(resolution.ToAction(reply.Entity.ID, ally));

        return reply;
    }

    protected string? WhyCannotSpendPreparation(Character character) =>
        _resourceService.GetValue(character, SpecialMoveRules.Preparation) > 0
            ? null
            : $"{character.Name} has no Preparation to spend. Take some with /downtime bolster";

    protected async Task<Character?> GetOwnCharacterAsync() =>
        await _characterService.GetCharacterFromUserAsync(GetUserId());
}
