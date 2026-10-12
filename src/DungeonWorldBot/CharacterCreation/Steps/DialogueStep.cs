using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using DungeonWorldBot.Services.Interactivity;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;

namespace DungeonWorldBot.CharacterCreation.Steps;

public readonly record struct StepResult<T>(bool IsCancelled, T? Value)
{
    public static StepResult<T> Cancelled => new(true, default);
}

/// <summary>
/// Asks the user a question in their DMs and keeps asking until they give a valid answer,
/// type /cancel, or stop responding.
/// </summary>
public abstract class DialogueStep<T>
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(1);

    protected const string CancelHint = "Type the /cancel command";

    protected readonly FeedbackService FeedbackService;
    protected readonly string Content;

    protected DialogueStep(FeedbackService feedbackService, string content)
    {
        FeedbackService = feedbackService;
        Content = content;
    }

    protected abstract Embed BuildPrompt();

    protected abstract bool TryParse(string input, [NotNullWhen(true)] out T? value, [NotNullWhen(false)] out string? problem);

    public async Task<StepResult<T>> RunAsync(
        InteractivityService interactivity,
        Snowflake channelId,
        Snowflake userId,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt();

        while (true)
        {
            var sent = await FeedbackService.SendPrivateEmbedAsync(userId, prompt, ct: ct);
            if (!sent.IsSuccess)
                return StepResult<T>.Cancelled;

            var reply = await interactivity.GetNextMessageAsync(channelId, sent.Entity.ID, ResponseTimeout, ct);

            // Errors and timeouts (no message) both end the dialogue
            if (!reply.IsSuccess || reply.Entity is null)
                return StepResult<T>.Cancelled;

            var input = reply.Entity.Content.Trim();
            if (input.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
                return StepResult<T>.Cancelled;

            if (TryParse(input, out var value, out var problem))
                return new StepResult<T>(false, value);

            await FeedbackService.SendMessageAsync(
                channelId,
                new FeedbackMessage($"There was a problem with your input: {problem}", Color.Red),
                ct: ct);
        }
    }
}
