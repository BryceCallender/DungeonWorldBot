using DungeonWorldBot.Data;

namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// The few places the dashboard changes the bot's database. Everywhere else it's opened read-only (see
/// <see cref="BotServices"/>), so a page can't change the game by accident; a write opens a scope of its own where it's
/// read-write, and the bot's services in that scope change it by the bot's rules.
/// </summary>
public sealed class GameWrites(IServiceScopeFactory scopes)
{
    /// <summary>
    /// A fresh scope each time, so a write never picks up anything else and is done before the page reloads.
    /// </summary>
    public GameWrite Open()
    {
        var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<GameAccess>().Writable = true;
        return new GameWrite(scope);
    }
}

/// <summary>
/// One write to the bot's database: its context and the bot's services over it, opened read-write.
/// </summary>
public sealed class GameWrite(AsyncServiceScope scope) : IAsyncDisposable
{
    public DungeonWorldContext Context => Get<DungeonWorldContext>();

    public T Get<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}

/// <summary>
/// Whether this scope's <see cref="DungeonWorldContext"/> is opened read-write, which only <see cref="GameWrites"/> sets.
/// </summary>
public sealed class GameAccess
{
    public bool Writable { get; set; }
}
