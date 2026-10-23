using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Gear;

/// <summary>
/// Every piece of custom gear.
/// </summary>
public class GearAutocomplete() : ClassSheetAutocomplete(null, null)
{
    public const string ID = "gear";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates() => GearLibrary.All.Select(g => new Suggestion(g.Name, g.Name));
}

/// <summary>
/// Every gear mode, like the Gem-Aspected Bow's gems.
/// </summary>
public class GearModeAutocomplete() : ClassSheetAutocomplete(null, null)
{
    public const string ID = "gear-mode";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates() =>
        GearLibrary.All.SelectMany(g => g.Modes.Select(m => new Suggestion(m.Name, $"{m.Name}: {m.Title} ({g.Name})")));
}

/// <summary>
/// Every counter gear keeps, like Dodge.
/// </summary>
public class GearCounterAutocomplete() : ClassSheetAutocomplete(null, null)
{
    public const string ID = "gear-counter";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates() => GearLibrary.Counters.Select(c => new Suggestion(c.Name, c.Name));
}
