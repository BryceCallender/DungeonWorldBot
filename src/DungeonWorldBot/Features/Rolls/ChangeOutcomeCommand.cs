using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// Right-click a roll → Apps → Change outcome, for when something the bot doesn't track makes it a hit (or a miss).
/// Asks which result it should count as with buttons, then why, handled by <see cref="ChangeOutcomeInteractions"/>.
/// </summary>
public class ChangeOutcomeCommand(
    ICommandContext context,
    FeedbackService feedbackService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    // Context menu commands take a single parameter, which Remora requires to be called "message"
    [Command("Change outcome")]
    [CommandType(ApplicationCommandType.Message)]
    public async Task<IResult> ChangeOutcomeAsync(IMessage message)
    {
        if (!message.Author.IsBot.OrDefault(false) || message.Embeds.FirstOrDefault() is not { } embed || RollOutcomeChange.Of(embed) is not { } shown)
            return await ReplyWithErrorAsync("That isn't a roll with a 10+ / 7-9 / 6- result.");

        var buttons = new IMessageComponent[]
        {
            Button(MoveOutcome.StrongHit, ButtonComponentStyle.Success, ChangeOutcomeInteractions.StrongHit, shown, message),
            Button(MoveOutcome.WeakHit, ButtonComponentStyle.Primary, ChangeOutcomeInteractions.WeakHit, shown, message),
            Button(MoveOutcome.Miss, ButtonComponentStyle.Danger, ChangeOutcomeInteractions.Miss, shown, message),
        };

        return await _feedbackService.SendContextualNeutralAsync(
            $"It's a {MoveRoller.Describe(shown)}. What should it count as?",
            options: new FeedbackMessageOptions(
                MessageFlags: MessageFlags.Ephemeral,
                MessageComponents: new IMessageComponent[] { new ActionRowComponent(buttons) }),
            ct: CancellationToken);
    }

    private static ButtonComponent Button(MoveOutcome outcome, ButtonComponentStyle style, string name, MoveOutcome shown, IMessage message) =>
        new(style, MoveRoller.Describe(outcome), CustomID: CustomIDHelpers.CreateButtonIDWithState(name, message.ID.ToString()), IsDisabled: outcome == shown);
}
