using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// The buttons <see cref="ChangeOutcomeCommand"/> asks with, then the form asking why. Each carries the roll message's ID
/// as its state, and the button's name says which result it changes to.
/// </summary>
public class ChangeOutcomeInteractions : InteractionGroup
{
    public const string StrongHit = "change-outcome-strong-hit";
    public const string WeakHit = "change-outcome-weak-hit";
    public const string Miss = "change-outcome-miss";

    // The form's text box, bound to the parameter of the same name
    private const string ReasonInput = "reason";

    private readonly IInteractionContext _context;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly IDiscordRestInteractionAPI _interactionAPI;
    private readonly IEncounterService _encounterService;
    private readonly IRollStatsService _rollStatsService;

    public ChangeOutcomeInteractions(
        IInteractionContext context,
        IDiscordRestChannelAPI channelAPI,
        IDiscordRestInteractionAPI interactionAPI,
        IEncounterService encounterService,
        IRollStatsService rollStatsService)
    {
        _context = context;
        _channelAPI = channelAPI;
        _interactionAPI = interactionAPI;
        _encounterService = encounterService;
        _rollStatsService = rollStatsService;
    }

    // A form has to be the interaction's first response, so Remora mustn't answer for us
    [Button(StrongHit)]
    [SuppressInteractionResponse(true)]
    public Task<Result> AskStrongHitAsync(string state) => AskWhyAsync(StrongHit, MoveOutcome.StrongHit, state);

    [Button(WeakHit)]
    [SuppressInteractionResponse(true)]
    public Task<Result> AskWeakHitAsync(string state) => AskWhyAsync(WeakHit, MoveOutcome.WeakHit, state);

    [Button(Miss)]
    [SuppressInteractionResponse(true)]
    public Task<Result> AskMissAsync(string state) => AskWhyAsync(Miss, MoveOutcome.Miss, state);

    [Modal(StrongHit)]
    [SuppressInteractionResponse(true)]
    public Task<Result> StrongHitAsync(string state, string reason) => ChangeAsync(state, MoveOutcome.StrongHit, reason);

    [Modal(WeakHit)]
    [SuppressInteractionResponse(true)]
    public Task<Result> WeakHitAsync(string state, string reason) => ChangeAsync(state, MoveOutcome.WeakHit, reason);

    [Modal(Miss)]
    [SuppressInteractionResponse(true)]
    public Task<Result> MissAsync(string state, string reason) => ChangeAsync(state, MoveOutcome.Miss, reason);

    private async Task<Result> AskWhyAsync(string name, MoveOutcome outcome, string state)
    {
        var form = new InteractionModalCallbackData(
            CustomIDHelpers.CreateModalIDWithState(name, state),
            $"Change to a {MoveRoller.Describe(outcome)}",
            new IMessageComponent[]
            {
                new ActionRowComponent(new IMessageComponent[]
                {
                    new TextInputComponent(
                        ReasonInput,
                        TextInputStyle.Short,
                        "Why?",
                        MinLength: 1,
                        MaxLength: 200,
                        IsRequired: true,
                        Value: default,
                        Placeholder: "e.g. Blessing of Luna"),
                }),
            });

        return await _interactionAPI.CreateInteractionResponseAsync(
            _context.Interaction.ID,
            _context.Interaction.Token,
            new InteractionResponse(InteractionCallbackType.Modal, new(form)),
            ct: CancellationToken);
    }

    private async Task<Result> ChangeAsync(string state, MoveOutcome outcome, string reason)
    {
        if (!Snowflake.TryParse(state, out var messageID) || !_context.TryGetChannelID(out var channelID) || !_context.TryGetUserID(out var userID))
            return await AnswerAsync("Couldn't find that roll.");

        var message = await _channelAPI.GetChannelMessageAsync(channelID, messageID.Value, CancellationToken);
        if (!message.IsSuccess || message.Entity.Embeds.FirstOrDefault() is not { } embed || RollOutcomeChange.Of(embed) is null)
            return await AnswerAsync("Couldn't find that roll. It may have been deleted.");

        var change = RollOutcomeChange.Apply(embed, outcome, reason.Trim(), userID);
        var edited = await _channelAPI.EditMessageAsync(channelID, messageID.Value, embeds: new[] { change.Embed }, ct: CancellationToken);
        if (!edited.IsSuccess)
            return await AnswerAsync($"Couldn't change the roll: {edited.Error.Message}");

        await _rollStatsService.ChangeOutcomeAsync(messageID.Value, change.Changed ? outcome : null, reason.Trim(), userID);

        // Does nothing for rolls outside a fight
        await _encounterService.ChangeOutcomeAsync(messageID.Value, MoveRoller.Describe(outcome));

        return await AnswerAsync(change.Changed
            ? $"Changed to a {MoveRoller.Describe(outcome)}: {reason.Trim()}"
            : $"Changed back to the {MoveRoller.Describe(outcome)} it rolled.");
    }

    // Replaces the buttons, so the same roll isn't changed twice by accident
    private async Task<Result> AnswerAsync(string content) =>
        await _interactionAPI.CreateInteractionResponseAsync(
            _context.Interaction.ID,
            _context.Interaction.Token,
            new InteractionResponse(
                InteractionCallbackType.UpdateMessage,
                new(new InteractionMessageCallbackData(Content: content, Components: Array.Empty<IMessageComponent>()))),
            ct: CancellationToken);
}
