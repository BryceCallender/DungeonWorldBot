using DungeonWorldBot.Data.Entities;
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

    // [Command("create")]
    // [SuppressInteractionResponse(true)]
    // public async Task<IResult> CharacterNameAsync()
    // {
    //     var embed = new Embed(Description: "Select a class below.");
    //     var options = new FeedbackMessageOptions(MessageComponents: new IMessageComponent[]
    //     {
    //         new ActionRowComponent(new[]
    //         {
    //             new SelectMenuComponent
    //             (
    //                 CustomIDHelpers.CreateSelectMenuID("class-dropdown"),
    //                 new ISelectOption[]
    //                 {
    //                     new SelectOption(ClassType.Ranger.ToString(), ClassType.Ranger.ToString()),
    //                     new SelectOption(ClassType.Channeler.ToString(), ClassType.Channeler.ToString()),
    //                     new SelectOption(ClassType.Paladin.ToString(), ClassType.Paladin.ToString()),
    //                     new SelectOption(ClassType.Necromancer.ToString(), ClassType.Necromancer.ToString()),
    //                     new SelectOption(ClassType.Slayer.ToString(), ClassType.Slayer.ToString()),
    //                 },
    //                 "Class...",
    //                 1,
    //                 1
    //             )
    //         })
    //     });
    //
    //     return await _feedback.SendPrivateEmbedAsync(
    //         _context.User.ID,
    //         embed, 
    //         options,
    //         CancellationToken
    //     );
    // }
    //

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
