using System.ComponentModel;
using System.Text;
using DungeonWorldBot.API.Models;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("roll")]
public class DiceRollCommand : CommandGroup
{
    private readonly Random _random;
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly IRollService _rollService;
    private readonly ICharacterService _characterService;
    
    public DiceRollCommand(
        Random random, 
        ICommandContext commandContext, 
        FeedbackService feedbackService,
        IRollService rollService,
        ICharacterService characterService)
    {
        _random = random;
        _context = commandContext;
        _feedbackService = feedbackService;
        _rollService = rollService;
        _characterService = characterService;
    }
    
    [Command("die")]
    [Description("Roll a dice like d6 or 2d6. Defaults to a 2d6")]
    public async Task<IResult> RollDiceAsync(string value = "2d6")
    {
        var roll = _rollService.Roll(value);

        return await ReplyWithRoll(roll);
    }

    [Command("stat")]
    [Description("Rolls a default 2d6 dice and adds your stat modifier")]
    public async Task<IResult> RollDiceWithStatAsync(StatType statType, string value = "2d6")
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);

        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to roll on stats. Try using /character create");

        var roll = _rollService.RollWithStat(value, character.Stats.Find(s => s.StatType == statType)!);

        return await ReplyWithRoll(roll, statType);
    }

    [Command("damage")]
    [Description("Rolls your respective classes damage dice")]
    public async Task<IResult> RollDamageDiceAsync(string value = "")
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);
        
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to roll for damage. Try using /character create");

        var roll = _rollService.Roll($"{character.Class.Damage}+{value}");

        return await ReplyWithRoll(roll, null);
    }

    private async Task<Result> ReplyWithFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "Dice rolling failed.",
            ct: CancellationToken
        );
    }
    
    private async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }

    private async Task<IResult> ReplyWithRoll(Roll roll)
    {
        return await ReplyWithRoll(roll, null);
    }

    private async Task<IResult> ReplyWithRoll(Roll roll, StatType? statType)
    {
        var total = roll.Total;
        var rollText = new StringBuilder($"{roll.Representation}\n\n");

        if (roll.Rolls.Count > 0 && !roll.Rolls.First().Representation.StartsWith("d"))
        {
            rollText.AppendLine($"Total Roll: {total}\n");
            rollText.Append($"{roll}={total}");
        }
        else
        {
            rollText.Append($"Total = {total}");
        }

        var avatar = CDN.GetUserAvatarUrl(_context.User, imageSize: 4096);

        if (!avatar.IsSuccess)
            avatar = CDN.GetDefaultUserAvatarUrl(_context.User, imageSize: 4096);
        
        if (!avatar.IsSuccess)
            return Result.FromError(avatar.Error);
        
        var embed = new Embed(
            Title: $"🎲 {(statType.HasValue ? statType.ToString() : "")} Roll(s)", 
            Description: rollText.ToString(),
            Colour: _feedbackService.Theme.Success,
            Thumbnail: new EmbedThumbnail(avatar.Entity.ToString())
        );

        return await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);
    }
}