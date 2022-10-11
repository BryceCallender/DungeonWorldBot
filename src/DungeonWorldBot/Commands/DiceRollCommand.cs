using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.SymbolStore;
using System.Security.Permissions;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

public class DiceRollCommand : CommandGroup
{
    private readonly Random _random;
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;

    public DiceRollCommand(Random random, ICommandContext commandContext, FeedbackService feedbackService)
    {
        _random = random;
        _context = commandContext;
        _feedbackService = feedbackService;
    }
    
    [Command("roll")]
    [Description("Roll a dice like d6 or 2d6")]
    public async Task<IResult> RollDiceAsync(string value)
    {
        var dice = value.ToLower().Split('+', StringSplitOptions.RemoveEmptyEntries);
        List<int> rolls = new List<int>();
        string seperatedRolls = string.Empty;

        if(dice.Length == 0)
        {
            return Result.FromSuccess();
        }
        else
        {
            foreach(var die in dice)
            {
                var rollConfigs = die.ToLower().Split('d', StringSplitOptions.RemoveEmptyEntries);

                var result = int.TryParse(rollConfigs[^1], out var faces);
                if (!result)
                {
                    return await ReplyWithFailureAsync();
                }

                List<int> rollValues;

                if (rollConfigs.Length == 1)
                {
                    rollValues = Roll(1, faces);
                }
                else
                {
                    result = int.TryParse(rollConfigs[0], out var rollCount);
                    if (!result)
                    {
                        return await ReplyWithFailureAsync();
                    }

                    rollValues = Roll(rollCount, faces);
                }


                seperatedRolls += $"({string.Join('+', rollValues)})";


                rolls.AddRange(rollValues);

                if(die != dice.Last())
                {
                    seperatedRolls += " + ";
                };
            };
        }

        return await ReplyWithRoll(value, rolls, seperatedRolls);


    }

    private List<int> Roll(int rollCount, int faces)
    {
        var results = new List<int>();

        for (var i = 0; i < rollCount; i++)
        {
            results.Add(_random.Next(1, faces));
        }

        return results;
    }
    
    private async Task<Result> ReplyWithFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "Dice rolling failed.",
            ct: CancellationToken
        );
    }

    private async Task<IResult> ReplyWithRoll(string roll, IReadOnlyCollection<int> rollValues, string seperatedRolls)
    {
        var total = rollValues.Sum();
        var rollText = $"{total}";

        if (rollValues.Count > 1)
        {
            rollText = $"{roll}\n\n";
            rollText += $"Total Roll: {total}\n\n";
            rollText += seperatedRolls;
            rollText += $"={total}";
        }
        
        
        var avatar = CDN.GetUserAvatarUrl(_context.User, imageSize: 4096);

        if (!avatar.IsSuccess)
            avatar = CDN.GetDefaultUserAvatarUrl(_context.User, imageSize: 4096);
        
        if (!avatar.IsSuccess)
            return Result.FromError(avatar.Error);
        
        var embed = new Embed(
            Title: "🎲 Roll(s)", 
            Description: rollText, 
            Colour: _feedbackService.Theme.Success,
            Thumbnail: new EmbedThumbnail(avatar.Entity.ToString())
        );

        return await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);
    }
}