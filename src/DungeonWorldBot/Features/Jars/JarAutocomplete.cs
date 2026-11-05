using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Rules;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Jars;

/// <summary>
/// The corpses in the player's Hexed Canopic Jars.
/// </summary>
public class JarAutocomplete(ContextInjectionService contextInjection, ICharacterService characterService)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "jar-corpse";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        JarRules.Corpses(character).Select(c => new Suggestion(c, c));

    protected override IEnumerable<Suggestion> AllCandidates() => [];
}
