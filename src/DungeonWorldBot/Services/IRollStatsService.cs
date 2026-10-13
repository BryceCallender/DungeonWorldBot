using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Services;

/// <summary>
/// How someone's 2d6 rolls have gone.
/// </summary>
public sealed record PlayerRollStats(Snowflake UserID, int Rolls, int StrongHits, int WeakHits, int Misses, int Natural12s, int Natural2s);

public interface IRollStatsService
{
    /// <summary>
    /// Records a roll the bot just posted, if it's a 2d6 roll.
    /// </summary>
    Task RecordAsync(Snowflake messageID, Snowflake channelID, Snowflake userID, DiceResult roll);

    /// <summary>
    /// Adds rolls found in chat history, skipping any already recorded.
    /// </summary>
    /// <returns>How many were new.</returns>
    Task<int> AddMissingAsync(IReadOnlyList<RollRecord> rolls);

    /// <returns>Everyone's stats, most rolls first.</returns>
    Task<List<PlayerRollStats>> GetStatsAsync();
}
