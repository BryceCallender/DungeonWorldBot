using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Npcs;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The enemies in the fights going on, still standing first. With more than one fight, each says which it's in.
/// </summary>
public class EnemyAutocomplete(IEncounterService encounterService) : IAutocompleteProvider
{
    public const string ID = "enemy";

    protected virtual bool IncludeDead => true;

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public virtual string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var running = await FightsAsync(options);
        var input = userInput.Trim();

        return running
            .SelectMany(e => e.Enemies)
            .Where(e => IncludeDead || e.CurrentHP > 0)
            .Where(e => e.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.CurrentHP == 0)
            .ThenBy(e => !e.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.Name, EnemySheet.NameOrder)
            .Take(MaxSuggestions)
            .Select(e => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                Shorten(running.Count > 1
                    ? $"{e.Name} · {EnemyConditions.Describe(e.Condition)} · {running.First(f => f.ID == e.EncounterID).Name}"
                    : $"{e.Name} · {EnemyConditions.Describe(e.Condition)}"),
                e.Name))
            .ToList();
    }

    /// <summary>
    /// The fights whose enemies to suggest: every one going on.
    /// </summary>
    protected virtual async Task<IReadOnlyList<Encounter>> FightsAsync(IReadOnlyList<IApplicationCommandInteractionDataOption> options) =>
        await encounterService.GetRunningAsync();

    // Discord takes names up to 100 characters
    private static string Shorten(string name) => name.Length <= 100 ? name : name[..99] + "…";
}

/// <summary>
/// The enemies in the fights going on that are still standing, for players picking a target.
/// </summary>
public class LivingEnemyAutocomplete(IEncounterService encounterService) : EnemyAutocomplete(encounterService)
{
    public new const string ID = "living-enemy";

    protected override bool IncludeDead => false;

    public override string Identity => ID;
}

/// <summary>
/// The enemies still standing in the fight whoever's acting is in, for a player's move or an NPC's roll to aim at: the
/// NPC picked in the command's "name" option, or else the player's own character. Every fight's when they're in none.
/// </summary>
public class TargetAutocomplete(
    IEncounterService encounterService,
    ICharacterService characterService,
    INpcService npcService,
    ContextInjectionService? contextInjection = null)
    : LivingEnemyAutocomplete(encounterService)
{
    public new const string ID = "target";

    public override string Identity => ID;

    protected override async Task<IReadOnlyList<Encounter>> FightsAsync(IReadOnlyList<IApplicationCommandInteractionDataOption> options)
    {
        Encounter? fight = null;
        if (NpcRollAutocomplete.ChosenName(options) is { } name)
        {
            if (await npcService.FindAsync(name) is { } npc)
                fight = await encounterService.GetForAsync(npcID: npc.ID);
        }
        else if (contextInjection?.Context is { } context
                 && context.TryGetUserID(out var userID)
                 && await characterService.GetCharacterFromUserAsync(userID) is { } character)
        {
            fight = await encounterService.GetForAsync(character.ID);
        }

        return fight is not null ? [fight] : await base.FightsAsync(options);
    }
}
