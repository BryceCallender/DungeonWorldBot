using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DungeonWorldBot.Data;

/// <summary>
/// Stores times as UTC, without an offset. SQLite only has text for them, and EF won't compare or sort text with an
/// offset in SQL; as plain UTC text, queries like "fights started during the session" run in the database. Every time
/// the bot saves is UTC already, so nothing is lost.
/// </summary>
public class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTime>
{
    public UtcDateTimeOffsetConverter()
        : base(at => at.UtcDateTime, utc => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))
    { }
}

/// <summary>
/// <see cref="UtcDateTimeOffsetConverter"/> for optional times.
/// </summary>
public class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, DateTime?>
{
    public NullableUtcDateTimeOffsetConverter()
        : base(
            at => at.HasValue ? at.Value.UtcDateTime : null,
            utc => utc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc)) : null)
    { }
}
