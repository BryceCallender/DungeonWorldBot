using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Dashboard;

/// <summary>
/// The dashboard's writes over a test database: the bot's services, as the dashboard registers them, each scope given a
/// context on the same in-memory database.
/// </summary>
public sealed class DashboardTestServices : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ServiceProvider _provider;

    public DashboardTestServices(TimeProvider? time = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(time ?? TimeProvider.System);
        services.AddScoped<GameAccess>();
        services.AddSingleton<GameWrites>();
        services.AddScoped(_ => _db.CreateContext());
        services.AddCoreServices();
        _provider = services.BuildServiceProvider();
    }

    public GameWrites Writes => _provider.GetRequiredService<GameWrites>();

    public DungeonWorldContext CreateContext() => _db.CreateContext();

    public async Task<Character> AddCharacterAsync(ulong id, string name)
    {
        await using var context = CreateContext();
        var character = new Character
        {
            ID = new Snowflake(id),
            Name = name,
            Class = new Class { Type = ClassType.Fighter },
            Health = new Health { CurrentHP = 10, MaxHP = 10 },
            Inventory = new Inventory(),
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }
}
