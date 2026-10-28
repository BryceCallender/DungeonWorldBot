using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;

namespace DungeonWorldBot.Features.GameMaster;

[Scoped<IGameMasterService>]
public class GameMasterService(ICommandContext context, IDiscordRestGuildAPI guildAPI, IOptions<BotSettings> settings) : IGameMasterService
{
    public string RoleName { get; } = settings.Value.DungeonMasterRoleName;

    public async Task<bool> IsGameMasterAsync(CancellationToken ct = default)
    {
        // Roles only exist in servers, so nobody is the DM in a DM channel
        if (!context.TryGetGuildID(out var guildId) || !context.TryGetUserID(out var userId))
            return false;

        var member = await guildAPI.GetGuildMemberAsync(guildId, userId, ct);
        if (!member.IsSuccess)
            return false;

        var roles = await guildAPI.GetGuildRolesAsync(guildId, ct);
        if (!roles.IsSuccess)
            return false;

        return roles.Entity.Any(r =>
            member.Entity.Roles.Contains(r.ID) && r.Name.Equals(RoleName, StringComparison.OrdinalIgnoreCase));
    }
}
