using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.GameMaster;

[Scoped<IGameMasterService>]
public class GameMasterService(IOperationContext context, IDiscordRestGuildAPI guildAPI, IOptions<BotSettings> settings) : IGameMasterService
{
    // Asked once per command: the condition on /dm commands and the command itself may both want to know
    private Task<bool>? _isGameMaster;

    public string RoleName { get; } = settings.Value.DungeonMasterRoleName;

    public Task<bool> IsGameMasterAsync(CancellationToken ct = default) => _isGameMaster ??= CheckRolesAsync(ct);

    public async Task<bool> MayChangeAsync(Snowflake? owner, CancellationToken ct = default) =>
        owner is not { } other
        || context.TryGetUserID(out var self) && other == self
        || await IsGameMasterAsync(ct);

    private async Task<bool> CheckRolesAsync(CancellationToken ct)
    {
        // Roles only exist in servers, so nobody is the DM in a DM channel
        if (!context.TryGetGuildID(out var guildId) || !context.TryGetUserID(out var userId))
            return false;

        var memberRoles = await MemberRolesAsync(guildId, userId, ct);
        if (memberRoles is null)
            return false;

        // Cached for ServiceExtensions.RoleListLifetime, so only the first DM command in a while asks Discord
        var roles = await guildAPI.GetGuildRolesAsync(guildId, ct);
        if (!roles.IsSuccess)
            return false;

        return roles.Entity.Any(r =>
            memberRoles.Contains(r.ID) && r.Name.Equals(RoleName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The member's roles: sent along with slash commands and buttons, so only fetched when they aren't.
    /// </summary>
    private async Task<IReadOnlyList<Snowflake>?> MemberRolesAsync(Snowflake guildId, Snowflake userId, CancellationToken ct)
    {
        if (context is IInteractionContext { Interaction.Member: { HasValue: true, Value: var member } })
            return member.Roles;

        var fetched = await guildAPI.GetGuildMemberAsync(guildId, userId, ct);
        return fetched.IsSuccess ? fetched.Entity.Roles : null;
    }
}
