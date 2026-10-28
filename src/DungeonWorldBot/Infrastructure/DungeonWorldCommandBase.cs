using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
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

    protected static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";

    protected async Task<IResult> ReplyAsync(string title, string description, IReadOnlyList<IEmbedField>? fields = null)
    {
        // Discord rejects embeds with titles over 256 characters or descriptions over 4096
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: Truncate(title, 256),
                Description: Truncate(description, 4096),
                Fields: fields?.ToList() ?? [],
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    protected async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync(error, ct: CancellationToken);
    }
}
