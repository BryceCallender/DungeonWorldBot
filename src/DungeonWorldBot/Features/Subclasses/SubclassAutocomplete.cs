using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Subclasses;

/// <summary>
/// Every subclass, labeled with the classes that can take it.
/// </summary>
public class SubclassAutocomplete : SuggestionAutocomplete
{
    public const string ID = "subclass";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates() =>
        SubclassLibrary.All.Select(s => new Suggestion(
            s.Name,
            s.Classes.Count > 0 ? $"{s.Name} ({string.Join("/", s.Classes)})" : s.Name));
}
