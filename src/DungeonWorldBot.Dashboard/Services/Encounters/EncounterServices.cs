using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Dashboard.Services.Encounters;

public static class EncounterServices
{
    /// <summary>
    /// Fights: the DM running them, the bestiary and replays of past ones, and moves made from tokens on a fight's
    /// board, which the bot rolls and boards hear how they went.
    /// </summary>
    public static IServiceCollection AddEncounters(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddSingleton(new EncounterDesk(databases.WritableGame()));
        services.AddScoped<BestiaryReader>();
        services.AddScoped<FightReplayReader>();

        services.AddScoped<CharacterMoves>();
        services.AddScoped<FightMoves>();
        services.AddSingleton<MoveResults>();
        services.AddHostedService(provider => provider.GetRequiredService<MoveResults>());
        return services;
    }
}
