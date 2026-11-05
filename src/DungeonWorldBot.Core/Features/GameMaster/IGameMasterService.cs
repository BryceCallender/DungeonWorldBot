using Remora.Rest.Core;

namespace DungeonWorldBot.Features.GameMaster;

public interface IGameMasterService
{
    /// <summary>
    /// The server role that marks someone as the DM.
    /// </summary>
    string RoleName { get; }

    /// <summary>
    /// Whether the person running the current command has the DM role.
    /// </summary>
    Task<bool> IsGameMasterAsync(CancellationToken ct = default);

    /// <summary>
    /// Whether the person running the current command may change the character of <paramref name="owner"/>: anyone can
    /// change their own (or leave it blank for their own), only the DM can change someone else's.
    /// </summary>
    Task<bool> MayChangeAsync(Snowflake? owner, CancellationToken ct = default);
}
