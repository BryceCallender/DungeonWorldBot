using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Data;

public class DungeonWorldContext : DbContext
{
    public DungeonWorldContext(DbContextOptions<DungeonWorldContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    public DbSet<Character> Characters => Set<Character>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        base.ConfigureConventions(builder);

        builder.Properties<Snowflake>().HaveConversion(typeof(SnowflakeConverter));
        builder.Properties<Snowflake?>().HaveConversion(typeof(NullableSnowflakeConverter));
    }
}