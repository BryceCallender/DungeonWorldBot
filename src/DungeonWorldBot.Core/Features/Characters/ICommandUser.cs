using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// Who ran the Discord command being handled, so a character they've taken over can stand in for their own.
/// </summary>
public interface ICommandUser
{
    /// <summary>
    /// Their user ID, or nothing outside of a command (like on the dashboard).
    /// </summary>
    Snowflake? ID { get; }
}
