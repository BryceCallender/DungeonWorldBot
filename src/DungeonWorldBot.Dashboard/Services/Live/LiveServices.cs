using DungeonWorldBot.Dashboard.Models.Live;
using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Live;

public static class LiveServices
{
    /// <summary>
    /// Watches both databases, so pages refresh themselves when the bot writes to its database, or anyone changes a
    /// canvas. Each is watched by name, so pages are told which changed.
    /// </summary>
    public static IServiceCollection AddLiveUpdates(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddSingleton(provider =>
            new DatabaseWatcher(
                [new(WatchedDatabase.Game, databases.GamePath), new(WatchedDatabase.Scenes, databases.CanvasPath)],
                provider.GetRequiredService<ILogger<DatabaseWatcher>>()));
        services.AddHostedService(provider => provider.GetRequiredService<DatabaseWatcher>());
        return services;
    }
}
