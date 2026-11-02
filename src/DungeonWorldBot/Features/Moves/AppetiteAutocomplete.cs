using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.DungeonWorld;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// The player's own appetites, for a Barbarian pursuing one. Everyone else gets no suggestions.
/// </summary>
public class AppetiteAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "appetite";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        Barbarian.Appetites(character).Select(a => new Suggestion(a, a));

    protected override IEnumerable<Suggestion> AllClassCandidates() => [];
}
