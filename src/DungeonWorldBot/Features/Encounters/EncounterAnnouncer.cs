using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Rolls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// Tells game chat about fights started or ended on the dashboard. The dashboard can't reach Discord, so it only
/// marks them in the database, and this notices them a moment later.
/// </summary>
public class EncounterAnnouncer(IServiceScopeFactory scopes, ILogger<EncounterAnnouncer> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    // When Discord won't take the message (no access to the channel, an outage), try again less often
    private static readonly TimeSpan Backoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = Interval;
            try
            {
                if (!await AnnounceAsync(stoppingToken))
                    delay = Backoff;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                // The database busy or mid-migration: the next look will likely do
                logger.LogWarning(e, "Couldn't check for fights to announce");
                delay = Backoff;
            }

            await Task.Delay(delay, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    /// <returns>False if Discord refused a message, so the next try should wait longer.</returns>
    private async Task<bool> AnnounceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var encounters = services.GetRequiredService<IEncounterService>();

        var waiting = await encounters.GetUnannouncedAsync();
        if (waiting.Count == 0)
            return true;

        if (services.GetRequiredService<IRollStatsService>().GameChatChannelID is not { } gameChat)
            return true;

        var channels = services.GetRequiredService<IDiscordRestChannelAPI>();
        var armor = services.GetRequiredService<IArmorService>();

        foreach (var encounter in waiting)
        {
            if (encounter.AnnouncedAt is null)
            {
                if (!await SendAsync(channels, gameChat, EncounterAnnouncements.Started(encounter), ct))
                    return false;

                encounter.AnnouncedAt = DateTimeOffset.UtcNow;
                await encounters.SaveAsync(encounter);
            }

            if (encounter.EndedAt is not null && encounter.EndAnnouncedAt is null)
            {
                // Clearing again on a retry finds nothing left, so a refused message is simply sent again later
                var cleared = await armor.EndFightForAsync(await encounters.LeavingAsync(encounter));
                if (!await SendAsync(channels, gameChat, EncounterAnnouncements.Ended(encounter, ArmorCommand.DescribeCleared(cleared)), ct))
                    return false;

                encounter.EndAnnouncedAt = DateTimeOffset.UtcNow;
                await encounters.SaveAsync(encounter);
            }
        }

        return true;
    }

    private async Task<bool> SendAsync(IDiscordRestChannelAPI channels, Snowflake channel, Remora.Discord.API.Objects.Embed embed, CancellationToken ct)
    {
        var sent = await channels.CreateMessageAsync(channel, embeds: new[] { embed }, ct: ct);
        if (sent.IsSuccess)
            return true;

        logger.LogWarning("Couldn't announce a fight in game chat: {Reason}", sent.Error.Message);
        return false;
    }
}
