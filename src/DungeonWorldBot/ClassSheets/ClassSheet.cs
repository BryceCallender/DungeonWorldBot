using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Moves;

namespace DungeonWorldBot.ClassSheets;

public enum MoveTier
{
    Starting,
    Level2To5,
    Level6To10,

    /// <summary>
    /// Things picked up outside of levelling, like a Shaman's spirit totems.
    /// </summary>
    Extra,
}

/// <summary>
/// One move from a class sheet. Learned moves are stored on the character as an <see cref="Ability"/> with the same name.
/// </summary>
public sealed record ClassMove(string Name, string Summary)
{
    public MoveTier Tier { get; init; } = MoveTier.Starting;

    /// <summary>
    /// A move the character must already have before taking this one.
    /// </summary>
    public string? Requires { get; init; }

    /// <summary>
    /// A move this one takes the place of. It's removed when this move is learned.
    /// </summary>
    public string? Replaces { get; init; }

    /// <summary>
    /// How the move is rolled, if it's rolled at all.
    /// </summary>
    public MoveDefinition? Roll { get; init; }

    /// <summary>
    /// A class resource (e.g. Control) or class stat (e.g. Lore) the move rolls with instead of a regular stat.
    /// </summary>
    public string? RollsWith { get; init; }

    /// <summary>
    /// For a choice that sets a class stat (like the Collector's Predilection setting Lore), the stat it stands for.
    /// </summary>
    public StatType? SetsClassStat { get; init; }

    /// <summary>
    /// Armor the move gives the character when picked during character creation.
    /// </summary>
    public int Armor { get; init; }

    /// <summary>
    /// A damage die that replaces the class's usual one, e.g. a winged Sky Dancer's d8.
    /// </summary>
    public string? DamageDie { get; init; }

    /// <summary>
    /// Uses or charges the move starts with, if it's limited.
    /// </summary>
    public int? Uses { get; init; }

    public bool IsAdvanced => Tier is MoveTier.Level2To5 or MoveTier.Level6To10;

    /// <summary>
    /// The moves named in <see cref="Requires"/>; having any one of them is enough.
    /// </summary>
    public IEnumerable<string> RequiredMoves =>
        Requires?.Split(" OR ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    public int MinLevel => Tier switch
    {
        MoveTier.Level2To5 => 2,
        MoveTier.Level6To10 => 6,
        _ => 1,
    };

    /// <summary>
    /// A rolled move, with its summary built from the roll so the two can't drift apart.
    /// </summary>
    public static ClassMove Rolled(
        string name,
        StatType? stat,
        string trigger,
        string strongHit,
        string weakHit,
        string miss = MoveDefinition.DefaultMiss,
        IReadOnlyList<string>? options = null,
        string? note = null,
        string? rollsWith = null,
        bool rollsDamage = false)
    {
        var rollsWithLabel = rollsWith ?? stat?.ToString().ToUpper() ?? "nothing";

        var summary = $"{trigger}, roll +{rollsWithLabel}. **10+:** {strongHit} **7-9:** {weakHit}";
        if (options is { Count: > 0 })
            summary += "\n" + string.Join('\n', options.Select(o => $"• {o}"));
        if (miss != MoveDefinition.DefaultMiss)
            summary += $"\n**6-:** {miss}";
        if (note is not null)
            summary += $"\n{note}";

        return new ClassMove(name, summary)
        {
            Roll = new MoveDefinition(name, stat, trigger, strongHit, weakHit, miss, options, rollsDamage),
            RollsWith = rollsWith,
        };
    }
}

/// <summary>
/// A choice on the sheet, like the Slayer's Manifestation. Each option is learned as its own move.
/// </summary>
public sealed record ClassFeature(string Name, string Prompt, IReadOnlyList<ClassMove> Options)
{
    /// <summary>
    /// How many options a new character picks.
    /// </summary>
    public int Picks { get; init; } = 1;

    /// <summary>
    /// Moves that let the character take more options from this list, and how many more.
    /// </summary>
    public IReadOnlyList<(string Move, int Count)> ExtraPicks { get; init; } = [];

    /// <summary>
    /// Whether the options not picked at creation can be taken later in place of an advanced move (the Golem's
    /// Immovable Object and Unstoppable Force).
    /// </summary>
    public bool LaterPicksAreAdvances { get; init; }

    public int AllowedPicks(IReadOnlyCollection<string> ownedMoves) =>
        Picks + ExtraPicks.Where(p => ownedMoves.Contains(p.Move, StringComparer.OrdinalIgnoreCase)).Sum(p => p.Count);

    public int TakenPicks(IReadOnlyCollection<string> ownedMoves) =>
        Options.Count(o => ownedMoves.Contains(o.Name, StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// A stat a class rolls that stands in for one of the six, chosen on the sheet (the Collector's Lore, the Golem's Force).
/// </summary>
/// <param name="Feature">The choice whose first pick decides which stat this is.</param>
public sealed record ClassStat(string Name, string Feature);

/// <summary>
/// A change to a resource's starting value or max from having a move.
/// </summary>
public sealed record ResourceBonus(string Move, int Start = 0, int Max = 0);

/// <summary>
/// A pool of points a class tracks, like the Battlemaster's Gambits.
/// </summary>
/// <param name="Start">What the resource starts at.</param>
/// <param name="GrantedBy">A move the character needs before they have this resource.</param>
public sealed record ClassResource(string Name, string Summary, int Start = 0, int? Max = null, string? GrantedBy = null)
{
    /// <summary>
    /// What resetting the resource sets it to, if not its starting value (a Captain starts with 1 Map but loses it all).
    /// </summary>
    public int? ResetTo { get; init; }

    /// <summary>
    /// Whether resetting every resource at once includes this one. Off for things that build up over the campaign, like Trade.
    /// </summary>
    public bool ResetWithAll { get; init; } = true;

    public IReadOnlyList<ResourceBonus> Bonuses { get; init; } = [];

    public int StartFor(IReadOnlyCollection<string> ownedMoves) => Start + BonusesFor(ownedMoves).Sum(b => b.Start);

    public int ResetValueFor(IReadOnlyCollection<string> ownedMoves) => ResetTo ?? StartFor(ownedMoves);

    public int? MaxFor(IReadOnlyCollection<string> ownedMoves) => Max + BonusesFor(ownedMoves).Sum(b => b.Max);

    private IEnumerable<ResourceBonus> BonusesFor(IReadOnlyCollection<string> ownedMoves) =>
        Bonuses.Where(b => ownedMoves.Contains(b.Move, StringComparer.OrdinalIgnoreCase));
}

public sealed class ClassSheet
{
    public ClassSheet(
        ClassType type,
        string source,
        IReadOnlyList<ClassMove> startingMoves,
        IReadOnlyList<ClassFeature> features,
        IReadOnlyList<ClassMove> level2To5,
        IReadOnlyList<ClassMove> level6To10,
        ClassMove? deathMove = null,
        IReadOnlyList<ClassResource>? resources = null,
        IReadOnlyList<ClassMove>? extras = null,
        ClassStat? classStat = null)
    {
        ClassStat = classStat;
        Type = type;
        Source = source;
        StartingMoves = startingMoves;
        Features = features;
        DeathMove = deathMove;
        Resources = resources ?? [];
        AdvancedMoves =
        [
            .. level2To5.Select(m => m with { Tier = MoveTier.Level2To5 }),
            .. level6To10.Select(m => m with { Tier = MoveTier.Level6To10 }),
        ];
        Extras = (extras ?? []).Select(m => m with { Tier = MoveTier.Extra }).ToList();
    }

    public ClassType Type { get; }

    /// <summary>
    /// Where the class comes from, e.g. "Grim World".
    /// </summary>
    public string Source { get; }

    public IReadOnlyList<ClassMove> StartingMoves { get; }
    public IReadOnlyList<ClassFeature> Features { get; }
    public IReadOnlyList<ClassMove> AdvancedMoves { get; }
    public ClassMove? DeathMove { get; }
    public IReadOnlyList<ClassResource> Resources { get; }
    public IReadOnlyList<ClassMove> Extras { get; }
    public ClassStat? ClassStat { get; }

    /// <summary>
    /// Every move a fresh character starts with, not counting feature choices.
    /// </summary>
    public IEnumerable<ClassMove> GrantedMoves => DeathMove is null ? StartingMoves : StartingMoves.Append(DeathMove);

    public IEnumerable<ClassMove> AllMoves =>
        GrantedMoves
            .Concat(Features.SelectMany(f => f.Options))
            .Concat(AdvancedMoves)
            .Concat(Extras);

    public ClassMove? FindMove(string name) =>
        AllMoves.FirstOrDefault(m => m.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public ClassFeature? FeatureFor(ClassMove move) =>
        Features.FirstOrDefault(f => f.Options.Contains(move));

    public ClassResource? FindResource(string name) =>
        Resources.FirstOrDefault(r => r.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The resources a character has, given the moves they know.
    /// </summary>
    public IEnumerable<ClassResource> ResourcesFor(IReadOnlyCollection<string> ownedMoves) =>
        Resources.Where(r => r.GrantedBy is null || ownedMoves.Contains(r.GrantedBy, StringComparer.OrdinalIgnoreCase));

    /// <returns>Why the move can't be learned right now, or null if it can.</returns>
    public string? WhyCannotLearn(ClassMove move, int level, IReadOnlyCollection<string> ownedMoves)
    {
        bool Owns(string name) => ownedMoves.Contains(name, StringComparer.OrdinalIgnoreCase);

        if (Owns(move.Name))
            return $"You already have {move.Name}.";

        if (level < move.MinLevel)
            return $"{move.Name} needs level {move.MinLevel}.";

        if (move.Requires is not null && !move.RequiredMoves.Any(Owns))
            return $"{move.Name} requires {move.Requires}.";

        var countsAsAdvance = move.IsAdvanced;
        if (FeatureFor(move) is { } feature && feature.TakenPicks(ownedMoves) >= feature.AllowedPicks(ownedMoves))
        {
            if (!feature.LaterPicksAreAdvances || level < 2)
                return $"You've already chosen your {feature.Name}.";

            countsAsAdvance = true;
        }

        if (countsAsAdvance)
        {
            // One advanced move per level gained
            var advancedTaken = AdvancesTaken(ownedMoves);
            if (advancedTaken >= level - 1)
                return $"You've already picked {advancedTaken} advanced move{(advancedTaken == 1 ? "" : "s")} for level {level}. Level up to pick another.";
        }

        return null;
    }

    /// <summary>
    /// How many advanced moves the character has taken, including choices taken later as advances.
    /// </summary>
    public int AdvancesTaken(IReadOnlyCollection<string> ownedMoves) =>
        AdvancedMoves.Count(m => ownedMoves.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
        + Features.Where(f => f.LaterPicksAreAdvances).Sum(f => Math.Max(0, f.TakenPicks(ownedMoves) - f.AllowedPicks(ownedMoves)));

    /// <summary>
    /// The advanced moves the character could pick right now, ignoring how many they've already taken.
    /// </summary>
    public IEnumerable<ClassMove> AvailableAdvancedMoves(int level, IReadOnlyCollection<string> ownedMoves) =>
        AdvancedMoves.Where(m =>
            m.MinLevel <= level
            && !ownedMoves.Contains(m.Name, StringComparer.OrdinalIgnoreCase)
            && (m.Requires is null || m.RequiredMoves.Any(r => ownedMoves.Contains(r, StringComparer.OrdinalIgnoreCase))));
}
