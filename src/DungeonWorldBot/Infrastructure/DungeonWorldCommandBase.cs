using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// What every command group shares: who ran the command and replying to them.
/// </summary>
public abstract class DungeonWorldCommandBase(ICommandContext context, FeedbackService feedbackService) : CommandGroup
{
    protected const string NoCharacterMessage = "No character exists. Try making one with /character create";

    protected readonly ICommandContext _context = context;
    protected readonly FeedbackService _feedbackService = feedbackService;

    /// <summary>
    /// The ID of the user who ran the command.
    /// </summary>
    protected Snowflake GetUserId()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

        return userId;
    }

    /// <summary>
    /// The most fields an embed can have.
    /// </summary>
    protected const int MaxEmbedFields = EmbedLimits.MaxFields;

    protected static string Truncate(string value, int length) => EmbedLimits.Truncate(value, length);

    protected Task<Result<IMessage>> ReplyAsync(string title, string description, IReadOnlyList<IEmbedField>? fields = null) =>
        ReplyWithEmbedAsync(new Embed(
            Title: title,
            Description: description,
            Fields: fields?.ToList() ?? [],
            Colour: _feedbackService.Theme.Primary));

    /// <summary>
    /// Replies with an embed, cut down to what Discord takes (see <see cref="EmbedLimits"/>).
    /// </summary>
    protected Task<Result<IMessage>> ReplyWithEmbedAsync(Embed embed, FeedbackMessageOptions? options = null) =>
        _feedbackService.SendContextualEmbedAsync(EmbedLimits.Fit(embed), options, CancellationToken);

    protected async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync(error, ct: CancellationToken);
    }
}
