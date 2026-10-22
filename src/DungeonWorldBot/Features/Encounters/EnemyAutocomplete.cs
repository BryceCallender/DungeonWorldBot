using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The enemies in the current fight, still standing first.
/// </summary>
public sealed class EnemyAutocomplete(IEncounterService encounterService) : IAutocompleteProvider
{
    public const string ID = "enemy";

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (await encounterService.GetActiveAsync() is not { } encounter)
            return [];

        var input = userInput.Trim();

        return encounter.Enemies
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
