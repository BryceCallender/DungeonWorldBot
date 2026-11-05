namespace DungeonWorldBot.Dashboard.Services.Party;

public static class PartyServices
{
    /// <summary>
    /// Reading the party's characters and their timelines, and the two things players change about them: their journals and pictures.
    /// </summary>
    public static IServiceCollection AddParty(this IServiceCollection services) =>
        services
            .AddScoped<PartyReader>()
            .AddScoped<CharacterHistory>()
            .AddSingleton<CharacterJournal>()
            .AddSingleton<PortraitStore>();
}
