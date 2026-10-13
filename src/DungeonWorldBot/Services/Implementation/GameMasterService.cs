using Microsoft.Extensions.Configuration;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;

namespace DungeonWorldBot.Services.Implementation;

public class GameMasterService : IGameMasterService
{
    private const string DefaultRoleName = "DM";

    private readonly ICommandContext _context;
    private readonly IDiscordRestGuildAPI _guildAPI;

    public GameMasterService(ICommandContext context, IDiscordRestGuildAPI guildAPI, IConfiguration configuration)
    {
        _context = context;
        _guildAPI = guildAPI;
        RoleName = configuration.GetValue<string?>("DungeonMasterRoleName") ?? DefaultRoleName;
    }

    public string RoleName { get; }

    public async Task<bool> IsGameMasterAsync(CancellationToken ct = default)
    {
        // Roles only exist in servers, so nobody is the DM in a DM channel
        if (!_context.TryGetGuildID(out var guildId) || !_context.TryGetUserID(out var userId))
            return false;

        var member = await _guildAPI.GetGuildMemberAsync(guildId, userId, ct);
        if (!member.IsSuccess)
            return false;

        var roles = await _guildAPI.GetGuildRolesAsync(guildId, ct);
        if (!roles.IsSuccess)
            return false;

        return roles.Entity.Any(r =>
            member.Entity.Roles.Contains(r.ID) && r.Name.Equals(RoleName, StringComparison.OrdinalIgnoreCase));
    }
}
