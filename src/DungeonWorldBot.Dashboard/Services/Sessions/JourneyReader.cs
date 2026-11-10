using DungeonWorldBot.Dashboard.Helpers;
using DungeonWorldBot.Dashboard.Models.Party;
using DungeonWorldBot.Dashboard.Models.Sessions;
using DungeonWorldBot.Dashboard.Services.Party;
using DungeonWorldBot.Dashboard.Services.Scenes;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Sessions;

/// <summary>
/// Where the party went over a session, for playing back on the campaign's map: a stop for each scene they fought on,
/// and each stop the DM added by hand, in order, with who went and what happened there.
/// </summary>
/// <remarks>
/// Everything belongs to the stop the party was at when it happened, from arriving until the next stop; anything from
/// before the first stop goes with it.
/// </remarks>
public class JourneyReader(
    SessionReader sessions,
    CharacterHistory history,
    PartyReader party,
    CampaignMap map,
    DungeonWorldContext context,
    TimeProvider time)
{
    public async Task<Journey> GetAsync(SessionSummary session)
    {
        var logs = await map.LogsAsync();
        return new Journey(session, await map.GetAsync(), await StopsAsync(session, await party.GetPartyAsync(), logs));
    }

    /// <summary>
    /// Every session's journey, oldest first, leaving out those with nowhere to go.
    /// </summary>
    public async Task<CampaignJourney> GetCampaignAsync()
    {
        var characters = await party.GetPartyAsync();
        var campaignMap = await map.GetAsync();
        var logs = await map.LogsAsync();

        List<Journey> journeys = [];
        foreach (var session in (await sessions.GetAllAsync()).OrderBy(s => s.Number))
            journeys.Add(new Journey(session, campaignMap, await StopsAsync(session, characters, logs)));

        return new CampaignJourney(campaignMap, [.. journeys.Where(j => j.Stops.Count > 0)]);
    }

    /// <summary>
    /// A fight's place: the scene it was on, or the fight's own name for one fought without a scene.
    /// </summary>
    public static string PlaceOf(Encounter fight) => fight.SceneName ?? fight.Name;

    private async Task<List<JourneyStop>> StopsAsync(
        SessionSummary session,
        IReadOnlyList<CharacterView> characters,
        IReadOnlyDictionary<int, List<LoggedStop>> logs)
    {
        var fights = (await sessions.GetFightsAsync(session)).OrderBy(f => f.StartedAt).ToList();
        var moments = (await ChangesAsync(session, characters)).Concat(fights.Select(f => FightEntry(session, f))).ToList();
        var everyone = characters.Select(c => c.Character.ID.Value).ToList();
        var logged = logs.GetValueOrDefault(session.Session.ID) ?? [];

        var arrivals = Merged([
            .. await FightArrivalsAsync(fights, everyone),
            .. logged.Select(l => new Arrival(l.Place, l.At, l.Who, l.ID)),
        ]);

        return arrivals.Select((arrival, i) =>
        {
            var from = i == 0 ? DateTimeOffset.MinValue : arrival.At;
            var until = i + 1 < arrivals.Count ? arrivals[i + 1].At : DateTimeOffset.MaxValue;
            var names = arrival.Who.Select(id => characters.PlayedBy(id)?.Name).OfType<string>().ToList();
            return new JourneyStop(
                arrival.Place,
                arrival.At,
                names,
                everyone.All(arrival.Who.Contains),
                [.. moments.Where(m => m.At >= from && m.At < until).OrderBy(m => m.At)],
                arrival.LoggedID);
        }).ToList();
    }

    // Who was in each fight; a fight with nobody recorded in it had the whole party
    private async Task<IEnumerable<Arrival>> FightArrivalsAsync(IReadOnlyList<Encounter> fights, IReadOnlyList<ulong> everyone)
    {
        var ids = fights.Select(f => f.ID).ToList();
        var fighters = (await context.EncounterParticipants.AsNoTracking()
                .Where(p => ids.Contains(p.EncounterID) && p.CharacterID != null)
                .Select(p => new { p.EncounterID, p.CharacterID })
                .ToListAsync())
            .ToLookup(p => p.EncounterID, p => p.CharacterID!.Value.Value);

        return fights.Select(f => new Arrival(PlaceOf(f), f.StartedAt, fighters[f.ID].Any() ? [.. fighters[f.ID]] : everyone));
    }

    // In order, back-to-back fights in one place by the same people being one stop
    private static List<Arrival> Merged(IEnumerable<Arrival> arrivals) =>
        arrivals.OrderBy(a => a.At)
            .Aggregate(new List<Arrival>(), (merged, next) =>
            {
                if (merged is not [.., var last] || !last.Continues(next))
                    merged.Add(next);
                return merged;
            });

    private static HistoryEntry FightEntry(SessionSummary session, Encounter fight) =>
        new(fight.StartedAt, HistoryKind.Fight, $"Fought: {fight.Name}", null, $"{session.Url}/fight/{fight.ID}");

    // What changed on the party, each line led by who it happened to: "Zephyr reached level 4"
    private async Task<IEnumerable<HistoryEntry>> ChangesAsync(SessionSummary session, IReadOnlyList<CharacterView> characters)
    {
        var changes = await history.GetChangesAsync(session.Session.StartedAt, session.Session.EndedAt ?? time.GetUtcNow());
        return changes.SelectMany(c => c.Entries.Select(e => e with { Text = Led(characters.PlayedBy(c.CharacterID.Value)?.Name, e.Text) }));
    }

    private static string Led(string? name, string text) =>
        name is null || text.Length == 0 ? text : $"{name} {char.ToLowerInvariant(text[0])}{text[1..]}";

    /// <param name="LoggedID">For a stop the DM added by hand; null for a fight's.</param>
    private record Arrival(string Place, DateTimeOffset At, IReadOnlyList<ulong> Who, string? LoggedID = null)
    {
        // The same fighters still in the same place, rather than somewhere new
        public bool Continues(Arrival next) =>
            LoggedID is null && next.LoggedID is null
            && string.Equals(Place, next.Place, StringComparison.OrdinalIgnoreCase)
            && Who.Order().SequenceEqual(next.Who.Order());
    }
}
