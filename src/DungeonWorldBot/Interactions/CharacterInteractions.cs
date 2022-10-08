using System.Drawing;
using DungeonWorldBot.Commands;
using DungeonWorldBot.Services;
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
    private readonly InteractivityService _interactivityService;

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
        ILogger<CharacterInteractions> logger,
        InteractivityService interactivityService)
    {
        _context = context;
        _channelAPI = channelAPI;
        _feedback = feedback;
        _logger = logger;
        _interactivityService = interactivityService;
    }
    
    [Modal("characterName")]
    public async Task<Result> OnModalSubmitAsync(string characterName)
    {
        _logger.LogInformation("Received modal response");
        _logger.LogInformation("Received input: {Input}", characterName);

        await _feedback.SendContextualEmbedAsync(new Embed(Description: $"Hello {characterName}", Colour: Color.Blue));

        return Result.FromSuccess();
    }
    
    [SelectMenu("class-dropdown")]
    public async Task<Result> SetClassAsync(IReadOnlyList<string> values)
    {
        if (!_context.Message.IsDefined(out var message))
        {
            return new InvalidOperationError("Interaction without a message?");
        }

        if (values.Count != 1)
        {
            return new InvalidOperationError("Only one element may be selected at any one time.");
        }

        var classType = values.Single();
        
        var embed = new Embed
        (
            Colour: Color.Lime,
            Description: $"You picked {classType}.",
            Footer: new EmbedFooter("Not your class? Select one below")
        );

        var components = new List<IMessageComponent>(message.Components.Value);
        var dropdown = (ISelectMenuComponent)((IActionRowComponent)components[0]).Components[0];
        
        var selected = dropdown.Options.Single(o => o.Value == classType);
        var newComponents = new List<IMessageComponent>
        {
            new ActionRowComponent(new[]
            {
                new SelectMenuComponent
                (
                    dropdown.CustomID,
                    dropdown.Options,
                    selected.Label,
                    dropdown.MinValues,
                    dropdown.MaxValues,
                    dropdown.IsDisabled
                )
            })
        };
        
        // await _channelAPI.EditMessageAsync
        // (
        //     _context.ChannelID,
        //     message.ID,
        //     embeds: new[] { embed },
        //     components: newComponents,
        //     ct: this.CancellationToken
        // );

        var test = await _interactivityService.GetNextMessageAsync(
            _context.ChannelID, 
            TimeSpan.FromSeconds(45), 
            CancellationToken
        );
        
        _logger.LogInformation("{Message}", test);
        
        return Result.FromSuccess();
    }
}