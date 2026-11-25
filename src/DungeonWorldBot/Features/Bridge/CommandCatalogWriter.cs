using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DungeonWorldBot.Features.Bridge;

/// <summary>
/// Writes the <see cref="CommandCatalog"/> beside the bot's database as it starts, for the dashboard to offer every
/// command it can run. Commands only change when the bot does, so once is enough.
/// </summary>
/// <param name="databasePath">The bot's SanaSolem.db.</param>
public class CommandCatalogWriter(string databasePath, IServiceScopeFactory scopes, ILogger<CommandCatalogWriter> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var catalog = scope.ServiceProvider.GetRequiredService<CommandCatalogBuilder>().Build();
            await catalog.WriteAsync(databasePath);
            logger.LogInformation("Told the dashboard about {Count} commands", catalog.Commands.Count);
        }
        // Never worth taking the bot down over: the dashboard just can't offer commands until it's written
        catch (Exception e)
        {
            logger.LogWarning(e, "Couldn't write the command catalog for the dashboard");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
