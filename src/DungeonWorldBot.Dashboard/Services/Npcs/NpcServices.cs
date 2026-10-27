using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Npcs;

public static class NpcServices
{
    /// <summary>
    /// The DM looking after the NPCs travelling with the party.
    /// </summary>
    public static IServiceCollection AddNpcs(this IServiceCollection services, DashboardDatabases databases) =>
        services.AddSingleton(new NpcDesk(databases.WritableGame()));
}
