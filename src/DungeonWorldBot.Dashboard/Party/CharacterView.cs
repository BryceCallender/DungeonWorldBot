using DungeonWorldBot.Dashboard.Reference;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// Everything about a character worth showing, worked out with the bot's own rules.
/// </summary>
public record CharacterView(
    Character Character,
    string ClassName,
    string Damage,
    IReadOnlyList<StatLine> Stats,
    ArmorView Armor,
    int StatPoints,
    IReadOnlyList<ResourceLine> Resources,
    SubclassView? Subclass,
    IReadOnlyList<SpellBook> SpellBooks,
    IReadOnlyList<GearView> Gear,
    IReadOnlyList<AbilityLine> Moves,
    IReadOnlyList<(string Feature, string Choice)> Picks)
{
    public string ID => Character.ID.Value.ToString();

    /// <summary>
    /// The character's name as it appears in addresses, e.g. "mr-hatter".
    /// </summary>
    public string Slug => Lookup.Slug(Name);

    public string Url => $"character/{Slug}";
    public string Name => Character.Name;
    public int Level => Character.Level;
    public int CurrentHP => Character.Health?.CurrentHP ?? 0;
    public int MaxHP => Character.Health?.MaxHP ?? 0;
    public double HealthPercent => MaxHP <= 0 ? 0 : Math.Clamp(CurrentHP / (double)MaxHP * 100, 0, 100);
    public IReadOnlyList<StatLine> Debilitated => Stats.Where(s => s.Debility is not null).ToList();
}
