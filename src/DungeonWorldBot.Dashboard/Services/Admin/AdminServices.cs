using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Admin;

public static class AdminServices
{
    /// <summary>
    /// An admin changing anything in either database, past the bot's rules.
    /// </summary>
    public static IServiceCollection AddAdmin(this IServiceCollection services, DashboardDatabases databases) =>
        services
            .AddSingleton(new AdminSchema(databases))
            .AddSingleton<AdminDesk>()
            .AddSingleton<AdminConsole>();
}
