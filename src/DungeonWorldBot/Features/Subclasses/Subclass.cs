using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Features.Subclasses;

/// <summary>
/// A move a subclass gives, unlocked when the character reaches its level.
/// </summary>
public sealed record SubclassMove(int Level, ClassMove Move)
{
    /// <summary>
    /// A move kept secret until the GM reveals it. It's shown as a teaser and never added to the character.
    /// </summary>
    public bool Hidden { get; init; }
}

/// <summary>
/// What a roll is for, so subclass bonuses (like a Boon of Luna) know whether they apply.
/// </summary>
public enum RollKind
{
    /// <summary>
    /// A move rolled with one of the six stats.
    /// </summary>
    Stat,
    Damage,
    LastBreath,
}

/// <summary>
/// A bonus to one roll, e.g. "Boon of Wrath +6".
/// </summary>
public sealed record RollBonus(string Source, int Amount);

/// <summary>
/// Bonuses a subclass member (the owner) gives to a roll by anyone in the party, including themselves.
/// </summary>
/// <param name="stat">The stat being rolled, for <see cref="RollKind.Stat"/> rolls.</param>
public delegate IEnumerable<RollBonus> RollBonusRule(Character owner, Character roller, RollKind kind, StatType? stat);

/// <summary>
/// A homebrew specialisation a character takes on top of their class, like the Ranger's Champion of Luna. Members are
/// marked by an ability named after the subclass, and its moves are learned as abilities as they unlock.
/// </summary>
public sealed class Subclass
{
    /// <param name="name">The subclass's name, also the name of the ability that marks a member.</param>
    /// <param name="title">A flavour line under the name, e.g. "Bearer of the Forgotten Moon".</param>
    /// <param name="source">Where it comes from, e.g. "Homebrew".</param>
    /// <param name="classes">The classes that can take it, or empty for any class.</param>
    public Subclass(
        string name,
        string title,
        string source,
        IReadOnlyList<ClassType> classes,
        string summary,
        IReadOnlyList<SubclassMove> moves)
    {
        Name = name;
        Title = title;
        Source = source;
        Classes = classes;
        Summary = summary;
        Moves = moves.OrderBy(m => m.Level).ToList();
    }

    public string Name { get; }
    public string Title { get; }
    public string Source { get; }
    public IReadOnlyList<ClassType> Classes { get; }
    public string Summary { get; }
    public IReadOnlyList<SubclassMove> Moves { get; }

    /// <summary>
    /// Bonuses members give to rolls, like a Boon of Luna.
    /// </summary>
    public RollBonusRule? Bonuses { get; init; }

    /// <summary>
    /// Anything about a member worth showing on their profile, like an active Vow.
    /// </summary>
    public Func<Character, string?>? Status { get; init; }

    /// <summary>
    /// Abilities the subclass uses to track state (like an active Vow), removed along with its moves when leaving it.
    /// </summary>
    public IReadOnlyList<string> StateAbilities { get; init; } = [];

    /// <summary>
    /// Where to find the subclass's own commands, shown when joining, e.g. "Use /subclass luna for Blood Moon and your Vow".
    /// </summary>
    public string? CommandHint { get; init; }

    public bool AllowedFor(ClassType type) => Classes.Count == 0 || Classes.Contains(type);

    /// <summary>
    /// The moves a member has at a level, not counting hidden ones.
    /// </summary>
    public IEnumerable<ClassMove> MovesAt(int level) => Moves.Where(m => !m.Hidden && m.Level <= level).Select(m => m.Move);

    public ClassMove? FindMove(string name) =>
        Moves.Where(m => !m.Hidden).Select(m => m.Move).FirstOrDefault(m => m.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
}
