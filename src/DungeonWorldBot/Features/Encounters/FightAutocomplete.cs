using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The fights going on, oldest first.
/// </summary>
public class FightAutocomplete(IEncounterService encounterService) : IAutocompleteProvider
{
    public const string ID = "fight";

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();

        return (await encounterService.GetRunningAsync())
            .Where(e => e.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(e => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                Shorten(e.SceneName is null ? e.Name : $"{e.Name} · {e.SceneName}"), e.Name))
            .ToList();
    }

    // Discord takes names up to 100 characters
    private static string Shorten(string name) => name.Length <= 100 ? name : name[..99] + "…";
}
