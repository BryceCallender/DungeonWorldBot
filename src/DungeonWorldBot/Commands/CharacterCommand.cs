using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("character")]
public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly InteractivityService _interactivityService;
    
    public CharacterCommand(
        ICommandContext context, 
        FeedbackService feedbackService, 
        ICharacterService characterService,
        InteractivityService interactivityService)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _interactivityService = interactivityService;
    }
    
    [Command("create")]
    public async Task<IResult> CreateCharacterAsync()
    {
        await _characterService.AddCharacterAsync(_context.User.ID, "");
        
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

        var embedFields = new List<IEmbedField>();
        
        embedFields.AddRange(character.Stats.Select(s => new EmbedField(s.StatType.ToString(), s.Value.ToString(), true)).ToList());

        var test = await _interactivityService.GetNextMessageAsync(_context.ChannelID, TimeSpan.FromSeconds(10));
        
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}",
                Fields: embedFields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
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