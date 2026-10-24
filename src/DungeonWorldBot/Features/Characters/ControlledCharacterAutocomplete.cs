using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// The characters the player runs alongside their own, by name.
/// </summary>
public class ControlledCharacterAutocomplete(ICharacterService characterService, ContextInjectionService? contextInjection = null) : IAutocompleteProvider
{
    public const string ID = "controlled-character";

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (contextInjection?.Context is not { } context || !context.TryGetUserID(out var userId))
            return [];

        var input = userInput.Trim();

        return (await characterService.GetControlledCharacterNamesAsync(userId))
            .Where(c => c.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .Take(MaxSuggestions)
            .Select(c => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                $"{c.Name} ({c.Class})", c.Name))
            .ToList();
    }
}
