namespace DungeonWorldBot.Services;

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
}
