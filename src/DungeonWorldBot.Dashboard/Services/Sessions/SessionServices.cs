namespace DungeonWorldBot.Dashboard.Services.Sessions;

public static class SessionServices
{
    /// <summary>
    /// The sessions played, and the one going on.
    /// </summary>
    public static IServiceCollection AddSessions(this IServiceCollection services) =>
        services.AddScoped<SessionReader>();
}
