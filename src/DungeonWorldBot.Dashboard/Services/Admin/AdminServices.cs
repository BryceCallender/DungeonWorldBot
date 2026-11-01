using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Admin;

public static class AdminServices
{
    /// <summary>
    /// The DM changing anything in either database, past the bot's rules.
    /// </summary>
    public static IServiceCollection AddAdmin(this IServiceCollection services, DashboardDatabases databases) =>
        services.AddSingleton(new AdminDesk(databases));
}
