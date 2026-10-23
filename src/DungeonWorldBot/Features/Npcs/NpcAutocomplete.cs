using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Npcs;

/// <summary>
/// The NPCs travelling with the party.
/// </summary>
public class NpcAutocomplete(INpcService npcService) : IAutocompleteProvider
{
    public const string ID = "npc";

    // Discord shows at most 25 suggestions
    private const int MaxSuggestions = 25;

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();

        return (await npcService.GetAllAsync())
            .Where(n => n.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => !n.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(n => n.Name)
            .Take(MaxSuggestions)
            .Select(n => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                n.Role is null ? n.Name : $"{n.Name} ({n.Role})", n.Name))
            .ToList();
    }
}
