namespace DungeonWorldBot.Dashboard.Services.Encounters;

public static class EncounterServices
{
    /// <summary>
    /// Fights: the DM running them, the bestiary and replays of past ones, and moves made from tokens on a fight's
    /// board, which the bot rolls and boards hear how they went.
    /// </summary>
    public static IServiceCollection AddEncounters(this IServiceCollection services)
    {
        services.AddSingleton<EncounterDesk>();
        services.AddScoped<BestiaryReader>();
        services.AddScoped<FightReplayReader>();

        // The bot's CharacterMoves comes with the rest of its services
        services.AddScoped<FightMoves>();
        services.AddSingleton<MoveResults>();
        services.AddHostedService(provider => provider.GetRequiredService<MoveResults>());
        return services;
    }
}
