using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Sessions;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Dashboard.Services.Shared;

public static class BotServices
{
    /// <summary>
    /// The bot's database, opened read-only so the dashboard can't change the game by accident, and the bot's own
    /// services over it, used only for reading. Only four things write to it, each with
    /// <see cref="DashboardDatabases.WritableGame"/>: CharacterJournal, for players' backstories, notes and goals;
    /// PortraitStore, for characters' pictures; EncounterDesk, for the DM running fights and keeping enemies and saved
    /// monsters; and NpcDesk, for the DM looking after the party's NPCs.
    /// </summary>
    public static IServiceCollection AddBotServices(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddSqlite<DungeonWorldContext>($"Data Source={databases.GamePath};Mode=ReadOnly");
        // Roll stats needs the game chat, set the same way as for the bot
        services.AddBotSettings();

        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IResourceService, ResourceService>();
        services.AddScoped<ILevelingService, LevelingService>();
        services.AddScoped<IRollStatsService, RollStatsService>();
        services.AddScoped<INpcService, NpcService>();
        services.AddScoped<IEncounterService, EncounterService>();
        services.AddScoped<IFightLogService, FightLogService>();
        services.AddScoped<ISpotlightService, SpotlightService>();
        services.AddScoped<ITimelineService, TimelineService>();
        services.AddScoped<ISessionService, SessionService>();
        return services;
    }
}
