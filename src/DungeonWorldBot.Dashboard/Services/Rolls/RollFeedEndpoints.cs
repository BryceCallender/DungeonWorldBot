using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Rolls;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Rolls;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Rolls;

/// <summary>
/// The game chat's newest rolls, for the dice that tumble onto every page when someone rolls in Discord.
/// </summary>
/// <remarks>
/// Pages ask again each time the live stream says the database changed, passing the newest roll they've seen. A page
/// that hasn't seen any yet only learns which roll is newest, so opening the dashboard doesn't replay old rolls.
/// </remarks>
public static class RollFeedEndpoints
{
    // More than this many between two changes (a /dice-stats scan of old chat) aren't news, so only the last few show
    private const int MaxRolls = 4;

    // How far back to look for the streak each new roll puts its roller on
    private const int StreakRolls = 200;

    public static RouteHandlerBuilder MapRollFeedEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/rolls/feed", async (ulong? after, DungeonWorldContext context, IRollStatsService rollStats,
            PartyReader party, PortraitStore portraits, DashboardSettings settings, Viewer viewer) =>
        {
            // Same rule as the roll stats: only game chat rolls count
            if (rollStats.GameChatChannelID is not { } gameChat)
                return new RollFeed(null, []);

            // EF won't order by IDs stored as ulong in SQLite, though the column is a plain INTEGER that sorts fine
            var newest = await context.Rolls
                .FromSql($"SELECT * FROM Rolls WHERE ChannelID = {gameChat.Value} ORDER BY MessageID DESC LIMIT {StreakRolls}")
                .AsNoTracking()
                .ToListAsync();

            var latest = newest.FirstOrDefault()?.MessageID.Value.ToString();
            var fresh = after is { } seen ? newest.Take(MaxRolls).Where(r => r.MessageID.Value > seen).Reverse().ToList() : [];
            if (fresh.Count == 0)
                return new RollFeed(latest, []);

            // Characters share their player's Discord ID
            var characters = (await party.GetPartyAsync()).ToDictionary(c => c.ID);
            var pictures = await portraits.UrlsAsync();

            return new RollFeed(latest, fresh
                .Select(r =>
                {
                    var player = r.UserID.Value;
                    var dice = MoveDice.Of(r);
                    // The streak as of this roll, counting back through theirs before it
                    var streak = RollStreak.Of(newest.Where(o => o.UserID == r.UserID && o.MessageID.Value <= r.MessageID.Value));
                    return new FeedRoll(
                        r.MessageID.Value.ToString(),
                        player == settings.DungeonMasterID ? "The DM" : characters.GetValueOrDefault(player.ToString())?.Name ?? "Someone",
                        pictures.GetValueOrDefault(player),
                        player == settings.DungeonMasterID,
                        viewer.Is(player),
                        r.First,
                        r.Second,
                        r.Total,
                        r.Move,
                        r.Stat is { } stat ? $"+{stat.ToString().ToUpper()}" : null,
                        dice.Outcome switch
                        {
                            MoveOutcome.StrongHit => "strong",
                            MoveOutcome.WeakHit => "weak",
                            _ => "miss",
                        },
                        streak is null ? null : $"{streak.Icon} {streak.Describe()}",
                        streak?.Hot,
                        // Their dice in their class's colour
                        characters.GetValueOrDefault(player.ToString()) is { } character
                            ? ClassSheetLibrary.For(character.Character.Class.Type)?.Color
                            : null);
                })
                .ToList());
        });
}
