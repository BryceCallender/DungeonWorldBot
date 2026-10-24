using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The fight a command is about, or why there isn't one.
/// </summary>
/// <param name="Error">What to tell whoever asked, or null when there's a fight.</param>
public record FightChoice(Encounter? Encounter, string? Error)
{
    /// <summary>
    /// The fight named, or else <paramref name="fallback"/>, or else the only one going on.
    /// </summary>
    /// <param name="noFight">What to say when nothing's going on.</param>
    public static async Task<FightChoice> PickAsync(IEncounterService service, string? name, string noFight, Encounter? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            return await service.FindRunningAsync(name) is { } named
                ? new FightChoice(named, null)
                : new FightChoice(null, $"There's no fight called {name.Trim()} going on.");
        }

        if (fallback is not null)
            return new FightChoice(fallback, null);

        var running = await service.GetRunningAsync();
        return running.Count switch
        {
            0 => new FightChoice(null, noFight),
            1 => new FightChoice(running[0], null),
            _ => new FightChoice(null, $"{running.Count} fights are going on: {string.Join(", ", running.Select(e => e.Name))}. Say which with fight."),
        };
    }
}
