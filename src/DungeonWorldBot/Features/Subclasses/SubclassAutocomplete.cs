using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Subclasses;

/// <summary>
/// Every subclass, labelled with the classes that can take it.
/// </summary>
public class SubclassAutocomplete() : ClassSheetAutocomplete(null, null)
{
    public const string ID = "subclass";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates() =>
        SubclassLibrary.All.Select(s => new Suggestion(
            s.Name,
            s.Classes.Count > 0 ? $"{s.Name} ({string.Join("/", s.Classes)})" : s.Name));
}
