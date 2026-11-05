using DungeonWorldBot.Features.GameMaster;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests;

/// <summary>
/// The DM check without Discord: whether whoever's running the command has the DM role, and who they are.
/// </summary>
public class FakeGameMaster(bool isGameMaster, Snowflake? self = null) : IGameMasterService
{
    public string RoleName => "DM";

    public Task<bool> IsGameMasterAsync(CancellationToken ct = default) => Task.FromResult(isGameMaster);

    public Task<bool> MayChangeAsync(Snowflake? owner, CancellationToken ct = default) =>
        Task.FromResult(owner is null || owner == self || isGameMaster);
}
