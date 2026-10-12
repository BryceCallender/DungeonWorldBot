using System.ComponentModel;
using DiceNotation;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("roll")]
public class DiceRollCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiceParser _diceParser;
    
    public DiceRollCommand(
        ICommandContext commandContext, 
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiceParser diceParser)
    {
        _context = commandContext;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _diceParser = diceParser;
    }
    
    [Command("die")]
    [Description("Roll a dice like d6 or 2d6. Defaults to a 2d6")]
    public async Task<IResult> RollDiceAsync(string value = "2d6")
    {
        if (!DiceHelper.TryRoll(_diceParser, value, out var result))
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

        if (!DiceHelper.TryRoll(_diceParser, DiceHelper.AddModifier(value, stat.Modifier), out var result))
            return await ReplyWithErrorAsync($"`{value}` is not a valid dice roll.");

        return await ReplyWithRoll(result, statType.ToString());
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
        
        if (!DiceHelper.TryRoll(_diceParser, rollString, out var result))
            return await ReplyWithErrorAsync($"`{rollString}` is not a valid dice roll.");

        return await ReplyWithRoll(result, null);
    }

    [Command("discern-realities")]
    [Description("Does a discern reality roll for you")]
    public async Task<IResult> RollDiscernRealitiesAsync()
    {
        if (!_context.TryGetUserID(out var userID))
        {
            throw new NotSupportedException();
        }
        
        var character = await _characterService.GetCharacterFromUserAsync(userID);

        var wisdom = character?.Stats.FirstOrDefault(s => s.StatType == StatType.Wis);
        if (wisdom is null)
            return await ReplyWithErrorAsync("You must have wisdom value to roll this");
        
        if (!DiceHelper.TryRoll(_diceParser, DiceHelper.AddModifier("2d6", wisdom.Modifier), out var result))
            return await ReplyWithErrorAsync("Dice rolling failed.");

        return await ReplyWithRoll(result, "Discern Realities");
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

    private async Task<IResult> ReplyWithRoll(DiceResult roll, string? rollDescription)
    {
        var rollText = DiceHelper.BuildDiceResult(roll);
        
        var embed = new Embed(
            Title: $"🎲 {rollDescription} Roll(s)", 
            Description: rollText,
            Colour: _feedbackService.Theme.Success
        );
    
        return await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);
    }
}