using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Models.Shared;

/// <summary>
/// Where the dashboard's two databases are: the bot's, which it mostly only reads, and its own, for scenes.
/// </summary>
/// <param name="GamePath">The bot's SanaSolem.db. Set DatabasePath to read another copy.</param>
/// <param name="CanvasPath">The dashboard's own canvases.db. Set CanvasDatabasePath to keep it elsewhere.</param>
public record DashboardDatabases(string GamePath, string CanvasPath)
{
    public static DashboardDatabases From(IConfiguration configuration, IHostEnvironment environment) => new(
        Path.GetFullPath(configuration.GetValue<string?>("DatabasePath")
            ?? Path.Combine(environment.ContentRootPath, "..", "DungeonWorldBot", "SanaSolem.db")),
        Path.GetFullPath(configuration.GetValue<string?>("CanvasDatabasePath")
            ?? Path.Combine(environment.ContentRootPath, "canvases.db")));

    /// <summary>
    /// The bot's database opened for writing, for the few things that change it (see <see cref="BotServices"/>).
    /// </summary>
    public DbContextOptions<DungeonWorldContext> WritableGame() =>
        new DbContextOptionsBuilder<DungeonWorldContext>()
            .UseSqlite($"Data Source={GamePath};Mode=ReadWrite")
            .Options;
}
