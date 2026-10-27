using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// The buttons <see cref="ChangeOutcomeCommand"/> asks with. Each carries the roll message's ID as its state.
/// </summary>
public class ChangeOutcomeInteractions : InteractionGroup
{
    public const string StrongHit = "change-outcome-strong-hit";
    public const string WeakHit = "change-outcome-weak-hit";
    public const string Miss = "change-outcome-miss";

    private readonly IInteractionContext _context;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly IDiscordRestInteractionAPI _interactionAPI;
    private readonly IEncounterService _encounterService;

    public ChangeOutcomeInteractions(
        IInteractionContext context,
        IDiscordRestChannelAPI channelAPI,
        IDiscordRestInteractionAPI interactionAPI,
        IEncounterService encounterService)
    {
        _context = context;
        _channelAPI = channelAPI;
        _interactionAPI = interactionAPI;
        _encounterService = encounterService;
    }

    [Button(StrongHit)]
    public Task<Result> StrongHitAsync(string state) => ChangeAsync(state, MoveOutcome.StrongHit);

    [Button(WeakHit)]
    public Task<Result> WeakHitAsync(string state) => ChangeAsync(state, MoveOutcome.WeakHit);

    [Button(Miss)]
    public Task<Result> MissAsync(string state) => ChangeAsync(state, MoveOutcome.Miss);

    private async Task<Result> ChangeAsync(string state, MoveOutcome outcome)
    {
        if (!Snowflake.TryParse(state, out var messageID) || !_context.TryGetChannelID(out var channelID) || !_context.TryGetUserID(out var userID))
            return await AnswerAsync("Couldn't find that roll.");

        var message = await _channelAPI.GetChannelMessageAsync(channelID, messageID.Value, CancellationToken);
        if (!message.IsSuccess || message.Entity.Embeds.FirstOrDefault() is not { } embed || RollOutcomeChange.Of(embed) is null)
            return await AnswerAsync("Couldn't find that roll. It may have been deleted.");

        var edited = await _channelAPI.EditMessageAsync(
            channelID,
            messageID.Value,
            embeds: new[] { RollOutcomeChange.Apply(embed, outcome, userID) },
            ct: CancellationToken);
        if (!edited.IsSuccess)
            return await AnswerAsync($"Couldn't change the roll: {edited.Error.Message}");

        // Does nothing for rolls outside a fight
        await _encounterService.ChangeOutcomeAsync(messageID.Value, MoveRoller.Describe(outcome));

        return await AnswerAsync($"Changed to a {MoveRoller.Describe(outcome)}.");
    }

    // Replaces the buttons, so the same roll isn't changed twice by accident
    private async Task<Result> AnswerAsync(string content) =>
        (Result)await _interactionAPI.EditOriginalInteractionResponseAsync(
            _context.Interaction.ApplicationID,
            _context.Interaction.Token,
            content,
            components: Array.Empty<IMessageComponent>(),
            ct: CancellationToken);
}
