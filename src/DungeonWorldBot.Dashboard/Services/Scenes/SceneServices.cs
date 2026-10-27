using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonWorldBot.Dashboard.Data;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Party;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Scenes;

public static class SceneServices
{
    /// <summary>
    /// Scenes, whose canvases are the dashboard's own, so they live in a database it can write, and are shared live
    /// between everyone who has one open.
    /// </summary>
    public static IServiceCollection AddScenes(this IServiceCollection services, DashboardDatabases databases)
    {
        services.AddSqlite<CanvasContext>($"Data Source={databases.CanvasPath}");
        services.AddScoped<CanvasStore>();
        services.AddSingleton<CanvasPresence>();

        // The default 32 KB is less than one long freehand stroke; CanvasStore.MaxItemLength sets the real limit per item
        services.AddSignalR(o => o.MaximumReceiveMessageSize = 512 * 1024)
            // Item types go back and forth as "stroke", "token" and "text" rather than numbers
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
        return services;
    }

    /// <summary>
    /// Creates the canvas database, or brings it up to date, before anyone opens a scene. Characters' pictures used to
    /// be kept there, so any still there are moved onto their rows first, before the update drops their table.
    /// </summary>
    public static void UpdateCanvasDatabase(this WebApplication app, DashboardDatabases databases)
    {
        var movedPortraits = PortraitStore.MoveFromCanvases(databases.GamePath, databases.CanvasPath);
        if (movedPortraits > 0)
            app.Logger.LogInformation("Moved {Count} character pictures from {Canvases} to {Database}", movedPortraits, databases.CanvasPath, databases.GamePath);

        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CanvasContext>().Database.Migrate();
    }
}
