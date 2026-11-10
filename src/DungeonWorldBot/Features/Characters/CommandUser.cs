using DungeonWorldBot.Infrastructure;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

[Scoped<ICommandUser>]
public class CommandUser(IOperationContext context) : ICommandUser
{
    public Snowflake? ID => context.TryGetUserID(out var id) ? id : null;
}
