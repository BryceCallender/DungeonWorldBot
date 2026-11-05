using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Rules;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Techniques;

/// <summary>
/// The Techniques the player's character knows.
/// </summary>
public class TechniqueAutocomplete(ContextInjectionService contextInjection, ICharacterService characterService)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "technique";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        (character.Techniques ?? []).Select(t => new Suggestion(t.Name, $"{t.Name} ({TechniqueRules.TagLine(t.Tags)})"));

    protected override IEnumerable<Suggestion> AllCandidates() => [];
}

/// <summary>
/// The tags the player's character can put on a Technique, including those their moves unlock.
/// </summary>
public class TechniqueTagAutocomplete(ContextInjectionService contextInjection, ICharacterService characterService)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "technique-tag";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        TechniqueRules.AvailableTags(character).Select(t => Suggest(t, character));

    protected override IEnumerable<Suggestion> AllCandidates() =>
        TechniqueRules.AllTags.Select(t => Suggest(t, null));

    private static Suggestion Suggest(TechniqueTag tag, Character? character) =>
        new(tag.ToString(), $"{tag}: {TechniqueRules.Describe(tag, character)}");
}
