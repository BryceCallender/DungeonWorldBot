//
//  SPDX-FileName: InteractiveCommands.cs
//  SPDX-FileCopyrightText: Copyright (c) Jarl Gullberg
//  SPDX-License-Identifier: MIT
//

using System.Linq;
using System.Threading.Tasks;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DungeonWorldBot.Commands;

public class InteractiveCommands : CommandGroup
{
    private readonly InteractionContext _context;
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestInteractionAPI _interactionAPI;

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractiveCommands"/> class.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="feedback">The feedback service.</param>
    /// <param name="interactionAPI">The interaction API.</param>
    public InteractiveCommands
    (
        InteractionContext context,
        FeedbackService feedback,
        IDiscordRestInteractionAPI interactionAPI
    )
    {
        _context = context;
        _feedback = feedback;
        _interactionAPI = interactionAPI;
    }

    [Command("characterName")]
    [SuppressInteractionResponse(true)]
    public async Task<IResult> CharacterNameAsync()
    {
        var components = new IMessageComponent[]
        {
            new ActionRowComponent(new[]
                {
                    new TextInputComponent(
                        "characterName", 
                        TextInputStyle.Short, 
                        "Character Name", 
                        1, 
                        32, 
                        true,
                        string.Empty,
                        "Short Text here"
                    ) 
                })
        };
        
        var data = new InteractionModalCallbackData(
            CustomIDHelpers.CreateModalID("characterName"), 
            "Set your character name!", 
            components
        );
        
        return await _interactionAPI.CreateInteractionResponseAsync(_context.ID, _context.Token, new InteractionResponse(InteractionCallbackType.Modal, new(data)));
    }
    
    /// <summary>
    /// Shows a modal.
    /// </summary>
    /// <returns>A result, indicating if the modal was sent successfully.</returns>
    [Command("modal")]
    [SuppressInteractionResponse(true)]
    public async Task<Result> ShowModalAsync()
    {
        if (_context is not InteractionContext interactionContext)
        {
            return (Result)await _feedback.SendContextualWarningAsync
            (
                "This command can only be used with slash commands.",
                _context.User.ID,
                new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral)
            );
        }

        var response = new InteractionResponse
        (
            InteractionCallbackType.Modal,
            new
            (
                new InteractionModalCallbackData
                (
                    CustomIDHelpers.CreateModalID("modal"),
                    "Test Modal",
                    new[]
                    {
                        new ActionRowComponent
                        (
                            new[]
                            {
                                new TextInputComponent
                                (
                                    "modal-text-input",
                                    TextInputStyle.Short,
                                    "Short Text",
                                    1,
                                    32,
                                    true,
                                    string.Empty,
                                    "Short Text here"
                                )
                            }
                        )
                    }
                )
            )
        );

        var result = await _interactionAPI.CreateInteractionResponseAsync
        (
            interactionContext.ID,
            interactionContext.Token,
            response,
            ct: this.CancellationToken
        );

        return result;
    }
}
