using Humanizer;
using Remora.Commands.Results;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Results;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

public class PostCommandHandler : IPostExecutionEvent
{
    private readonly ICommandPrefixMatcher _prefix;
    private readonly IDiscordRestChannelAPI _channels;
    private readonly IDiscordRestInteractionAPI _interactions;

    public PostCommandHandler
    (
        ICommandPrefixMatcher prefix,
        IDiscordRestChannelAPI channels,
        IDiscordRestInteractionAPI interactions
    )
    {
        _prefix = prefix;
        _channels = channels;
        _interactions = interactions;
    }

    public async Task<Result> AfterExecutionAsync(ICommandContext context, IResult commandResult, CancellationToken ct = default)
    {
        if (commandResult.IsSuccess)
            return Result.FromSuccess();
        
        var error = GetDeepestError(commandResult);

        if (context is MessageContext mc)
        {
            var prefixResult = await _prefix.MatchesPrefixAsync(mc.Message.Content.Value, ct);
        
            if (!prefixResult.IsDefined(out var prefix) || !prefix.Matches || mc.Message.Content.Value.Length <= prefix.ContentStartIndex)
                return Result.FromSuccess();
            
            if (error is CommandNotFoundError)
                return Result.FromSuccess();
            
        }

        if (error is ExceptionError er)
            await _channels.CreateMessageAsync(context.ChannelID, $"{er.Message}", ct: ct);

        if (commandResult.Error is AggregateError ag && ag.Errors.First().Error is ConditionNotSatisfiedError)
        {
            var message = error!.Message;

            var responseMessage = error switch
            {
                PermissionDeniedError pne => $"As much as I'd love to, you're missing permissions to {pne.Permissions.Select(p => p.Humanize(LetterCasing.Title)).Humanize()}!",
                _                         => message
            };

            if (context is not InteractionContext ic)
                await _channels.CreateMessageAsync(context.ChannelID, responseMessage, ct: ct);
            else
                await _interactions.CreateFollowupMessageAsync(ic.ApplicationID, ic.Token, responseMessage, flags: MessageFlags.Ephemeral, ct: ct);
        }
        
        return Result.FromSuccess();
    }
    
    private static IResultError? GetDeepestError(IResult error)
        => error.IsSuccess 
            ? error.Error
            : error.Error is AggregateError ag      
                ? GetDeepestError(ag.Errors.First())
                : error.Inner is null 
                    ? error.Error 
                    : GetDeepestError(error.Inner!);
}