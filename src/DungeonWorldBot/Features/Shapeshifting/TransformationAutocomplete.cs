using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Shapeshifting;

/// <summary>
/// The forms the player's character knows, whatever their class.
/// </summary>
public class TransformationAutocomplete(ContextInjectionService contextInjection, ICharacterService characterService)
    : SuggestionAutocomplete
{
    public const string ID = "transformation";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates() => [];

    protected override async Task<IEnumerable<Suggestion>> CandidatesAsync()
    {
        if (contextInjection.Context is not { } context || !context.TryGetUserID(out var userId))
            return [];

        var character = await characterService.GetCharacterMovesAsync(userId);
        return character?.Transformations.Select(t => new Suggestion(t.Name, t.IsActive ? $"{t.Name} (current)" : t.Name)) ?? [];
    }
}
