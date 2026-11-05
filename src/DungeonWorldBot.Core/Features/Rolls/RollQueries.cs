using DungeonWorldBot.Data.Entities;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// Rolls by when they were made. A roll's message ID starts with the time it was sent, so a time is turned into the
/// first ID sent at it, and the database compares and sorts IDs (see SnowflakeConverter) rather than reading every roll.
/// </summary>
public static class RollQueries
{
    /// <summary>
    /// The rolls made at or after <paramref name="from"/>.
    /// </summary>
    public static IQueryable<RollRecord> Since(this IQueryable<RollRecord> rolls, DateTimeOffset from)
    {
        var first = FirstAt(from);
        return rolls.Where(r => r.MessageID >= first);
    }

    /// <summary>
    /// The rolls made at or before <paramref name="to"/>.
    /// </summary>
    public static IQueryable<RollRecord> Until(this IQueryable<RollRecord> rolls, DateTimeOffset to)
    {
        // IDs carry the time to the millisecond, so everything up to the next millisecond's first ID
        var after = FirstAt(to.AddMilliseconds(1));
        return rolls.Where(r => r.MessageID < after);
    }

    public static IQueryable<RollRecord> OldestFirst(this IQueryable<RollRecord> rolls) =>
        rolls.OrderBy(r => r.MessageID);

    public static IQueryable<RollRecord> NewestFirst(this IQueryable<RollRecord> rolls) =>
        rolls.OrderByDescending(r => r.MessageID);

    private static Snowflake FirstAt(DateTimeOffset at) => Snowflake.CreateTimestampSnowflake(at, Constants.DiscordEpoch);
}
