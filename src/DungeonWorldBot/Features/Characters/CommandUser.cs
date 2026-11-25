using DungeonWorldBot.Infrastructure;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// Who ran the command, or no one outside a command (as at startup or in a background worker).
/// </summary>
/// <remarks>
/// Read from the context as it's asked, not taken when built: the bridge sets the context after the scope is made, and
/// asking for <c>IOperationContext</c> itself throws where there's none.
/// </remarks>
[Scoped<ICommandUser>]
public class CommandUser(ContextInjectionService injection) : ICommandUser
{
    public Snowflake? ID => injection.Context?.TryGetUserID(out var id) == true ? id : null;
}
