using DungeonWorldBot.Infrastructure;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

[Scoped<ICommandUser>]
public class CommandUser(ContextInjectionService contextInjection) : ICommandUser
{
    // Read through ContextInjectionService rather than taking IOperationContext, which throws when there's no command,
    // like for the warm-up at startup
    public Snowflake? ID => contextInjection.Context is { } context && context.TryGetUserID(out var id) ? id : null;
}
