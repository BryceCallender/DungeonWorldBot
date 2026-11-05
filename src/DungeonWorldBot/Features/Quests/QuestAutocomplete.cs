using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Rules;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Quests;

/// <summary>
/// The boons a Paladin can choose for a quest.
/// </summary>
public class QuestBoonAutocomplete : SuggestionAutocomplete
{
    public const string ID = "quest-boon";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates() =>
        QuestRules.AllBoons.Select(b => new Suggestion(b.Name, $"{b.Name}: {QuestRules.Describe(b, null)}"));
}

/// <summary>
/// The vows the GM can set for a quest.
/// </summary>
public class QuestVowAutocomplete : SuggestionAutocomplete
{
    public const string ID = "quest-vow";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates() =>
        QuestRules.AllVows.Select(v => new Suggestion(v, v));
}
