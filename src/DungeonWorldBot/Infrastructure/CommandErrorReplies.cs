using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// Tells whoever ran a command when it didn't work, instead of leaving them on "thinking..." or "interaction failed":
/// why, when it's something they can do about (like a DM command without the DM role), or that something went wrong.
/// Commands that fail for a reason the player should see reply themselves and succeed, so anything here is unexpected
/// and is logged for the dashboard's health page.
/// </summary>
public class CommandErrorReplies(FeedbackService feedbackService, ILogger<CommandErrorReplies> logger)
    : IPreparationErrorEvent, IPostExecutionEvent
{
    public Task<Result> PreparationFailed(IOperationContext context, IResult preparationResult, CancellationToken ct = default) =>
        ReplyToFailureAsync(preparationResult, "that command", ct);

    public Task<Result> AfterExecutionAsync(ICommandContext context, IResult commandResult, CancellationToken ct = default) =>
        ReplyToFailureAsync(commandResult, $"/{CommandTelemetry.NameOf(context)}", ct);

    private async Task<Result> ReplyToFailureAsync(IResult result, string command, CancellationToken ct)
    {
        if (result.IsSuccess)
            return Result.Success;

        if (Find<NotGameMasterError>(result) is { } notGameMaster)
            return await ReplyAsync(notGameMaster.Message, ct);

        var thrown = Find<ExceptionError>(result);
        if (thrown?.Exception is DbUpdateConcurrencyException)
        {
            // The dashboard saved the same enemy, NPC or HP a moment before: nothing was saved, so it's safe to run again
            logger.LogWarning("{Command} lost a race to save", command);
            return await ReplyAsync($"That changed at the same moment, likely on the dashboard. Nothing was saved, so run {command} again.", ct);
        }

        if (thrown is not null)
            logger.LogError(thrown.Exception, "{Command} threw", command);
        else
            logger.LogError("{Command} failed: {Reason}", command, result.Error?.Message);

        return await ReplyAsync($"Something went wrong running {command}. Try again, and tell the DM if it keeps happening.", ct);
    }

    private async Task<Result> ReplyAsync(string message, CancellationToken ct) =>
        (Result)await feedbackService.SendContextualErrorAsync(
            message,
            options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral),
            ct: ct);

    /// <summary>
    /// The first error of type <typeparamref name="TError"/> in the result or anything it wraps.
    /// </summary>
    public static TError? Find<TError>(IResult result) where TError : class, IResultError
    {
        for (IResult? current = result; current is not null; current = current.Inner)
        {
            if (current.Error is TError error)
                return error;
        }

        return null;
    }
}
