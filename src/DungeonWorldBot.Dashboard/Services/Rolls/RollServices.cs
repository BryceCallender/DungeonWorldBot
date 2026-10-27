namespace DungeonWorldBot.Dashboard.Services.Rolls;

public static class RollServices
{
    /// <summary>
    /// How the dice have gone, for the stats page and the roll feed.
    /// </summary>
    public static IServiceCollection AddRolls(this IServiceCollection services) =>
        services.AddScoped<RollReader>();
}
