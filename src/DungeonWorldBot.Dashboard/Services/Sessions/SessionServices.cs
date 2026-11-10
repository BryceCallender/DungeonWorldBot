namespace DungeonWorldBot.Dashboard.Services.Sessions;

public static class SessionServices
{
    /// <summary>
    /// The sessions played, the one going on, and where the party went in each.
    /// </summary>
    public static IServiceCollection AddSessions(this IServiceCollection services) =>
        services
            .AddScoped<SessionReader>()
            .AddScoped<JourneyReader>();
}
