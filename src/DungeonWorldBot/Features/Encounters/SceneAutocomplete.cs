using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// The dashboard's encounter scenes, most recently drawn on first.
/// </summary>
public sealed class SceneAutocomplete(SceneDirectory scenes) : IAutocompleteProvider
{
    public const string ID = "scene";

    // Discord shows at most 25 suggestions, with names up to 100 characters
    private const int MaxSuggestions = 25;
    private const int MaxNameLength = 100;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();

        return (await scenes.GetEncounterScenesAsync())
            .Where(s => s.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSuggestions)
            .Select(s => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                s.Name.Length <= MaxNameLength ? s.Name : s.Name[..(MaxNameLength - 1)] + "…", s.ID))
            .ToList();
    }
}
