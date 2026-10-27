using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Party;

public static class PartyServices
{
    /// <summary>
    /// Reading the party's characters, and the two things players change about them: their journals and pictures.
    /// </summary>
    public static IServiceCollection AddParty(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddScoped<PartyReader>();
        services.AddSingleton(new CharacterJournal(databases.WritableGame()));
        services.AddSingleton(new PortraitStore(databases.WritableGame()));
        return services;
    }
}
