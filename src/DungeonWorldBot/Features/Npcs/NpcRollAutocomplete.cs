using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

namespace DungeonWorldBot.Features.Npcs;

/// <summary>
/// What the NPC picked in the command's "name" option can roll with: stats, loyalty, skills and damage.
/// </summary>
public sealed class NpcRollAutocomplete(INpcService npcService) : IAutocompleteProvider
{
    public const string ID = "npc-roll";

    public string Identity => ID;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        if (ChosenName(options) is not { } name || await npcService.FindAsync(name) is not { } npc)
            return [];

        var input = userInput.Trim();
        return NpcRolls.Options(npc)
            .Where(o => o.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .Select(o => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(
                o.Modifier is { } mod ? $"{o.Name} {NpcSheet.Signed(mod)}" : $"{o.Name} ({o.Dice})", o.Name))
            .ToList();
    }

    // Subcommands nest their options, so look all the way down
    private static string? ChosenName(IEnumerable<IApplicationCommandInteractionDataOption> options)
    {
        foreach (var option in options)
        {
            if (option.Name == "name" && option.Value.HasValue && option.Value.Value.TryPickT0(out var name, out _))
                return name;

            if (option.Options.HasValue && ChosenName(option.Options.Value) is { } nested)
                return nested;
        }

        return null;
    }
}
