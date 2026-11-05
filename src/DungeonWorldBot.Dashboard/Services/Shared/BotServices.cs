using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Shared;

public static class BotServices
{
    /// <summary>
    /// The bot's database, opened read-only so the dashboard can't change the game by accident, and the bot's own
    /// services over it (the same ones the bot registers). Only <see cref="GameWrites"/> opens it read-write, for the
    /// desks that change it by the bot's rules: CharacterJournal, for players' backstories, notes and goals; PortraitStore,
    /// for characters' pictures; EncounterDesk, for the DM running fights and keeping enemies and saved monsters; and
    /// NpcDesk, for the DM looking after the party's NPCs. AdminDesk goes around them, for the DM to change anything
    /// directly.
    /// </summary>
    public static IServiceCollection AddBotServices(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddScoped<GameAccess>();
        services.AddSingleton<GameWrites>();
        services.AddDbContext<DungeonWorldContext>((provider, options) => options.UseSqlite(
            provider.GetRequiredService<GameAccess>().Writable
                ? $"Data Source={databases.GamePath};Mode=ReadWrite"
                : $"Data Source={databases.GamePath};Mode=ReadOnly"));

        // Roll stats needs the game chat, and the DM's settings their file beside the bot's database, set the same way
        // as for the bot
        services.AddBotSettings();
        services.PostConfigure<BotSettings>(settings =>
            settings.GameSettingsPath = Path.Combine(Path.GetDirectoryName(databases.GamePath)!, settings.GameSettingsPath));

        return services.AddCoreServices();
    }
}
