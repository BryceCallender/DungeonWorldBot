using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Shapeshifting;

/// <summary>
/// The forms the player's character knows.
/// </summary>
public sealed class TransformationAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "transformation";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        (character.Transformations ?? []).Select(t => new Suggestion(t.Name, t.IsActive ? $"{t.Name} (current)" : t.Name));

    protected override IEnumerable<Suggestion> AllClassCandidates() => [];
}
