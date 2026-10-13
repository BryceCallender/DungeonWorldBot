using DungeonWorldBot.ClassSheets;

namespace DungeonWorldBot.Spells;

/// <summary>
/// Every spell, labelled with its list. Names on more than one list (like Light) send "Name (List)".
/// </summary>
public sealed class SpellAutocomplete() : ClassSheetAutocomplete(null, null)
{
    public const string ID = "spell";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Data.Entities.Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates()
    {
        var shared = SpellLists.All.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return SpellLists.All.Select(s => new Suggestion(
            shared.Contains(s.Name) ? s.Qualified : s.Name,
            $"{s.Name} ({s.List} {s.LevelLabel.ToLower()})"));
    }
}
