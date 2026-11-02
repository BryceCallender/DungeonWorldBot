using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Rules;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Quests;

/// <summary>
/// The boons a Paladin can choose for a quest.
/// </summary>
public class QuestBoonAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "quest-boon";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates() =>
        QuestRules.AllBoons.Select(b => new Suggestion(b.Name, $"{b.Name}: {QuestRules.Describe(b, null)}"));
}

/// <summary>
/// The vows the GM can set for a quest.
/// </summary>
public class QuestVowAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "quest-vow";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates() =>
        QuestRules.AllVows.Select(v => new Suggestion(v, v));
}
