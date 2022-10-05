using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    
    public CharacterCommand(ICommandContext context, FeedbackService feedbackService, ICharacterService characterService)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
    }
    
    [Command("create")]
    public async Task<IResult> CreateCharacterAsync(string name)
    {
        await _characterService.AddCharacterAsync(name);
        
        return Result.FromSuccess();
    }

    [Command("profile")]
    public async Task<IResult> ShowCharacterSheetAsync()
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);
        if (character is null)
        {
            return await ReplyWithFailureAsync();
        }
        
        return Result.FromSuccess();
    }

    private async Task<Result> ReplyWithFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "No character exists. Try making one with !create",
            ct: CancellationToken
        );
    }
    
    private async Task<Result> ReplyWithError(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }
}