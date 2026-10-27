using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Live;

public static class LiveServices
{
    /// <summary>
    /// Watches both databases, so pages refresh themselves when the bot writes to its database, or anyone changes a
    /// canvas.
    /// </summary>
    public static IServiceCollection AddLiveUpdates(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddSingleton(provider =>
            new DatabaseWatcher([databases.GamePath, databases.CanvasPath], provider.GetRequiredService<ILogger<DatabaseWatcher>>()));
        services.AddHostedService(provider => provider.GetRequiredService<DatabaseWatcher>());
        return services;
    }
}
