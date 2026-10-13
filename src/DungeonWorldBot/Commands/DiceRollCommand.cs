using d20Tek.DiceNotation.Results;
using System.ComponentModel;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Moves;
using DungeonWorldBot.Services;
using DungeonWorldBot.Subclasses;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("roll")]
public class DiceRollCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiceRoller _diceRoller;
    private readonly IRollStatsService _rollStatsService;
    private readonly IDamageService _damageService;
    
    public DiceRollCommand(
        ICommandContext commandContext, 
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiceRoller diceRoller,
        IRollStatsService rollStatsService,
        IDamageService damageService)
    {
        _context = commandContext;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _diceRoller = diceRoller;
        _rollStatsService = rollStatsService;
        _damageService = damageService;
    }
    
    [Command("die")]
    [Description("Roll a dice like d6 or 2d6. Defaults to a 2d6")]
    public async Task<IResult> RollDiceAsync(string value = "2d6")
    {
        if (!_diceRoller.TryRoll(value, out var result))
            return await ReplyWithErrorAsync($"`{value}` is not a valid dice roll.");

        return await ReplyWithRoll(result);
    }

    [Command("stat")]
    [Description("Rolls a default 2d6 dice and adds your stat modifier")]
    public async Task<IResult> RollDiceWithStatAsync(StatType statType, string value = "2d6")
    {
        if (!_context.TryGetUserID(out var userID))
        {
            throw new NotSupportedException();
        }
    
        var character = await _characterService.GetCharacterFromUserAsync(userID);
    
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to roll on stats. Try using /character create");

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        // Subclass bonuses, like a Boon of Aptitude or its Echo from a party member.
        var bonuses = await _damageService.BonusesAsync(character, RollKind.Stat, statType);

        if (!_diceRoller.TryRoll(DiceHelper.AddModifier(value, stat.Modifier + bonuses.Sum(b => b.Amount)), out var result))
            return await ReplyWithErrorAsync($"`{value}` is not a valid dice roll.");

        var includes = bonuses.Count > 0
            ? $"\nIncludes {string.Join(", ", bonuses.Select(b => $"{b.Source} {(b.Amount >= 0 ? "+" : "")}{b.Amount}"))}"
            : null;

        return await ReplyWithRoll(result, statType.ToString(), includes);
    }
    
    [Command("damage")]
    [Description("Rolls your respective classes damage dice. Can also add on modifiers as needed.")]
    public async Task<IResult> RollDamageDiceAsync(string modifiers = "")
    {
        if (!_context.TryGetUserID(out var userID))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userID);
        
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to roll for damage. Try using /character create");
    
        var rollString = $"{character.DamageModifier ?? character.Class.Damage}";
        if (modifiers.Length > 0)
        {
            rollString += $"+{modifiers}";
        } 
        
        if (!_diceRoller.TryRoll(rollString, out var result))
            return await ReplyWithErrorAsync($"`{rollString}` is not a valid dice roll.");

        return await ReplyWithRoll(result, null);
    }

    private async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }

    private async Task<IResult> ReplyWithRoll(DiceResult roll)
    {
        return await ReplyWithRoll(roll, null);
    }

    /// <param name="details">Extra lines under the dice, like the bonuses included.</param>
    private async Task<IResult> ReplyWithRoll(DiceResult roll, string? rollDescription, string? details = null)
    {
        var outcome = MoveRoller.OutcomeOf(roll);

        var embed = new Embed(
            Title: $"🎲 {rollDescription} Roll(s)",
            Description: DiceHelper.BuildDiceResult(roll) + details,
            Fields: outcome is { } band
                ? new List<IEmbedField> { new EmbedField(MoveRoller.Describe(band), MoveRoller.GeneralMeaning(band)) }
                : new Optional<IReadOnlyList<IEmbedField>>(),
            Colour: outcome is { } o ? MoveRoller.ColourFor(o) : _feedbackService.Theme.Success
        );
    
        var reply = await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);
        if (reply.IsSuccess && _context.TryGetUserID(out var userID) && _context.TryGetChannelID(out var channelID))
            await _rollStatsService.RecordAsync(reply.Entity.ID, channelID, userID, roll);

        return reply;
    }
}