using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Data;

/// <summary>
/// Stores snowflakes as signed numbers. SQLite only has those, so EF refuses to compare or sort unsigned ones in SQL;
/// as signed numbers, queries like "rolls since" and "newest first" run in the database. Discord's IDs stay below
/// long.MaxValue until 2084, so every one is stored as the same number either way.
/// </summary>
public class SnowflakeConverter : ValueConverter<Snowflake, long>
{
    public SnowflakeConverter()
        : base(sf => (long)sf.Value, value => DiscordSnowflake.New((ulong)value))
    { }
}

/// <summary>
/// <see cref="SnowflakeConverter"/> for optional snowflakes.
/// </summary>
public class NullableSnowflakeConverter : ValueConverter<Snowflake?, long?>
{
    public NullableSnowflakeConverter()
        : base(
            sf => sf.HasValue ? (long)sf.Value.Value : null,
            value => value.HasValue ? DiscordSnowflake.New((ulong)value.Value) : null)
    { }
}
