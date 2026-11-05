namespace DungeonWorldBot.Dashboard.Services.Npcs;

public static class NpcServices
{
    /// <summary>
    /// The DM looking after the NPCs traveling with the party.
    /// </summary>
    public static IServiceCollection AddNpcs(this IServiceCollection services) =>
        services.AddSingleton<NpcDesk>();
}
