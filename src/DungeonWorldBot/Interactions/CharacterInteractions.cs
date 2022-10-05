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
        FeedbackService feedback
    )
    {
        _context = context;
        _channelAPI = channelAPI;
        _feedback = feedback;
    }
    
    [Modal("character")]
    public Task<Result> OnModalSubmitAsync(string modalTextInput)
    {
        return Task.FromResult(Result.FromSuccess());
    }
}