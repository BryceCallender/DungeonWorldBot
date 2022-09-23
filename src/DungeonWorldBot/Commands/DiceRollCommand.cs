using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DDBot.Commands;

public class DiceRollCommand : CommandGroup
{
    private readonly FeedbackService _feedbackService;

    public DiceRollCommand(FeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }
    
    [Command("roll")]
    public async Task<IResult> RollDiceAsync(string value)
    {
        var rollConfigs = value.ToLower().Split('d', StringSplitOptions.RemoveEmptyEntries);
        if (rollConfigs.Length == 0)
        {
            return Result.FromSuccess();
        }

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

        return await ReplyWithRoll(rollValues);
    }

    private static List<int> Roll(int rollCount, int faces)
    {
        var random = new Random();
        var results = new List<int>();

        for (var i = 0; i < rollCount; i++)
        {
            results.Add(random.Next(1, faces));
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

    private async Task<IResult> ReplyWithRoll(IReadOnlyCollection<int> rollValues)
    {
        var total = rollValues.Sum();
        var rolls = string.Join('+', rollValues);
        rolls += $"={total}";
        
        var embed = new Embed(Title: "Rolls", Description: $"Total Roll: {total}\n\n{rolls}", Colour: _feedbackService.Theme.Success);

        return await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);
    }
}