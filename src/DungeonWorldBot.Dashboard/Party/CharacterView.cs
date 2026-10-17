using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Dashboard.Party;

/// <param name="IgnoredBy">The move that lets the character ignore the debility, like the Slayer's Tenacity.</param>
public sealed record StatLine(StatType Type, int Value, int Modifier, DebilityType? Debility, string? IgnoredBy)
{
    public int RollModifier => Debility is not null && IgnoredBy is null ? Modifier + DebilityRules.Penalty : Modifier;
}

public sealed record ResourceLine(string Name, string Summary, int Value, int? Max);

/// <param name="GrantsSpells">The spell list this move lets the character cast from, like God Amidst the Wastes.</param>
public sealed record AbilityLine(string Name, string? Description, int Uses, int? MaxUses, SpellList? GrantsSpells = null);

public sealed record SpellBook(SpellList List, string GrantedBy, int CasterLevel, int LevelsUsed, IReadOnlyList<Spell> Spells)
{
    public int LevelsAllowed => CasterLevel + 1;

    public bool OverLimit => LevelsUsed > LevelsAllowed;
}

/// <param name="Unlocked">Whether the character has reached the move's level.</param>
/// <param name="Hidden">Kept secret until the GM reveals it.</param>
public sealed record SubclassMoveLine(int Level, string Name, string Summary, bool Unlocked, bool Hidden);

public sealed record SubclassView(string Name, string Title, string? Status, IReadOnlyList<SubclassMoveLine> Moves);

public sealed record GearView(string Name, string ModeKind, string? Mode, string? ModeTitle, string? Effect);

public sealed record ArmorView(int Physical, int Magical, int Divine, int BasePhysical, int BaseMagical, int BaseDivine, IReadOnlyList<string> Bonuses);

/// <summary>
/// Everything about a character worth showing, worked out with the bot's own rules.
/// </summary>
public sealed record CharacterView(
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
    IReadOnlyList<AbilityLine> Moves)
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
