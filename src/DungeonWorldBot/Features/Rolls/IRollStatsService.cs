using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// How someone's 2d6 rolls have gone.
/// </summary>
public record PlayerRollStats(Snowflake UserID, int Rolls, int StrongHits, int WeakHits, int Misses, int Natural12s, int Natural2s);

public interface IRollStatsService
{
    /// <summary>
    /// The game chat channel, set with the GameChatChannelId setting. Only rolls made there count toward stats.
    /// </summary>
    Snowflake? GameChatChannelID { get; }

    /// <summary>
    /// Records a roll the bot just posted, if it's a 2d6 roll.
    /// </summary>
    /// <param name="label">The move and stat it was for, usually read off the roll's embed with <see cref="RollLabel.Of"/>.</param>
    Task RecordAsync(Snowflake messageID, Snowflake channelID, Snowflake userID, DiceResult roll, RollLabel? label = null);

    /// <summary>
    /// Adds rolls found in chat history, skipping any already recorded. Recorded rolls missing their move or stat get
    /// them from the scan.
    /// </summary>
    /// <returns>How many were new.</returns>
    Task<int> AddMissingAsync(IReadOnlyList<RollRecord> rolls);

    /// <returns>Everyone's stats from rolls in the game chat, most rolls first.</returns>
    Task<List<PlayerRollStats>> GetStatsAsync();
}
