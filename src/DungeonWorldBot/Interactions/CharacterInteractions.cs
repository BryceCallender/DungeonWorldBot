using Microsoft.Extensions.Logging;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DungeonWorldBot.Interactions;

public class CharacterInteractions : InteractionGroup
{
    private readonly InteractionContext _context;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly FeedbackService _feedback;
    private readonly ILogger<CharacterInteractions> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ColourDropdownInteractions"/> class.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="channelAPI">The channel API.</param>
    /// <param name="feedback">The feedback service.</param>
    public CharacterInteractions
    (
        InteractionContext context,
        IDiscordRestChannelAPI channelAPI,
        FeedbackService feedback,
        ILogger<CharacterInteractions> logger
    )
    {
        _context = context;
        _channelAPI = channelAPI;
        _feedback = feedback;
        _logger = logger;
    }
    
    [Modal("characterName")]
    public Task<Result> OnModalSubmitAsync(string characterName)
    {
        _logger.LogInformation("Received modal response");
        _logger.LogInformation("Received input: {Input}", characterName);

        return Task.FromResult(Result.FromSuccess());
    }
}