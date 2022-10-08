using DungeonWorldBot.Services;
using DSharpPlus.CommandsNext;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using System.Drawing;
using Remora.Discord.Gateway;
using Remora.Discord.API.Objects;
using Remora.Discord.Interactivity.Services;
using Remora.Discord.Interactivity;
using DungeonWorldBot.Services.Implementation.Steps;
using DungeonWorldBot.Interactions;
using CommandGroup = Remora.Commands.Groups.CommandGroup;
using Remora.Discord.API.Abstractions.Rest;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Commands;

public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly IServiceProvider _services;

    public CharacterCommand(ICommandContext context, FeedbackService feedbackService, ICharacterService characterService, IDiscordRestChannelAPI channelAPI, IServiceProvider services)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _channelAPI = channelAPI;   
        _services = services;
    }

    [Command("create")]
    public async Task<IResult> CreateCharacterAsync()
    {
        InteractivityService interactivity = new InteractivityService(_services, new InteractiveMessageTracker(), _channelAPI, _feedbackService);
        
        string input = string.Empty;

        var inputStep = new TextStep("What is your characters name?", _feedbackService, null);

        inputStep.OnValidResult += (result) => input = result;



        var userChannel = _context.User.ID;

        var inputDialogueHandler = new DialogueHandler(interactivity, userChannel, _context.User, inputStep);

        bool succeeded = await inputDialogueHandler.ProcessDialogue(_feedbackService).ConfigureAwait(false);

        if (!succeeded) { return Result.FromSuccess(); }

        await _feedbackService.SendPrivateMessageAsync(userChannel, new FeedbackMessage(input, Color.Yellow));
        
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
            "No character exists. Try making one with /create",
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