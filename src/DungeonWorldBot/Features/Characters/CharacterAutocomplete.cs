using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// The party's characters, by name.
/// </summary>
public class CharacterAutocomplete(ICharacterService characterService) : IAutocompleteProvider
{
    public const string ID = "character";

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();

        return (await characterService.GetCharacterNamesAsync())
            .Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => !c.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Name)
            .Take(MaxSuggestions)
            .Select(c => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                $"{c.Name} ({c.Class})", c.Name))
            .ToList();
    }
}