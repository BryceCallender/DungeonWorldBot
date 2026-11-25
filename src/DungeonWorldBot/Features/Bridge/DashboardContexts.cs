using Remora.Commands.Services;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Bridge;

/// <summary>
/// A command being found for the dashboard (see <see cref="CommandBridge"/>): as if the player had typed it in game
/// chat, so who ran it and where replies go work as they do for a typed command.
/// </summary>
public record DashboardMessageContext(IPartialMessage Message, Optional<Snowflake> GuildID) : MessageContext(Message, GuildID);

/// <summary>
/// A command run for the dashboard (see <see cref="CommandBridge"/>). Replies go to game chat, as for a typed command,
/// and the bridge tells the dashboard what went wrong itself rather than <see cref="Infrastructure.CommandErrorReplies"/>.
/// </summary>
public record DashboardCommandContext(IPartialMessage Message, Optional<Snowflake> GuildID, PreparedCommand Command)
    : TextCommandContext(Message, GuildID, Command);
