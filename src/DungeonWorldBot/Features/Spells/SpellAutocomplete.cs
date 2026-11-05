using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Spells;

/// <summary>
/// Every spell, labeled with its list. Names on more than one list (like Light) send "Name (List)".
/// </summary>
public class SpellAutocomplete : SuggestionAutocomplete
{
    public const string ID = "spell";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates()
    {
        var shared = SpellLists.All.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return SpellLists.All.Select(s => new Suggestion(
            shared.Contains(s.Name) ? s.Qualified : s.Name,
            $"{s.Name} ({s.List} {s.LevelLabel.ToLower()})"));
    }
}
