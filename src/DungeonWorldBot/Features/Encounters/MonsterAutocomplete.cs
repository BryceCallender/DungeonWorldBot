using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The monsters the GM has saved.
/// </summary>
public class MonsterAutocomplete(IEncounterService encounterService) : IAutocompleteProvider
{
    public const string ID = "monster";

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();

        return (await encounterService.GetMonstersAsync())
            .Where(m => m.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => !m.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(m => m.Name)
            .Take(MaxSuggestions)
            .Select(m => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                $"{m.Name} · {m.MaxHP} HP", m.Name))
            .ToList();
    }
}
