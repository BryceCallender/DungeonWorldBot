using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The enemies in the current fight, still standing first.
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
        if (await encounterService.GetActiveAsync() is not { } encounter)
            return [];

        var input = userInput.Trim();

        return encounter.Enemies
            .Where(e => IncludeDead || e.CurrentHP > 0)
            .Where(e => e.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.CurrentHP == 0)
            .ThenBy(e => !e.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => e.Name, EnemySheet.NameOrder)
            .Take(MaxSuggestions)
            .Select(e => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                $"{e.Name} · {EnemyConditions.Describe(e.Condition)}", e.Name))
            .ToList();
    }
}

/// <summary>
/// The enemies in the current fight that are still standing, for players picking a target.
/// </summary>
public class LivingEnemyAutocomplete(IEncounterService encounterService) : EnemyAutocomplete(encounterService)
{
    public new const string ID = "living-enemy";

    protected override bool IncludeDead => false;

    public override string Identity => ID;
}
