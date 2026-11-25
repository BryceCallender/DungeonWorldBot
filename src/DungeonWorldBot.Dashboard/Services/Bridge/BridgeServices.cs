using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Bridge;

public static class BridgeServices
{
    /// <summary>
    /// Running the bot's commands from the dashboard, from the list of them it writes beside its database.
    /// </summary>
    public static IServiceCollection AddBridge(this IServiceCollection services, DashboardDatabases databases) =>
        services
            .AddSingleton(new CommandCatalogReader(databases.GamePath))
            .AddScoped<CommandDesk>();
}
