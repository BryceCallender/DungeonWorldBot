using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Classes;

/// <summary>
/// A move from another class's sheet.
/// </summary>
public record ForeignMove(ClassSheet Sheet, ClassMove Move);

/// <summary>
/// Taking moves from other classes' sheets through moves like the Ranger's Special Trick.
/// </summary>
public static partial class Multiclassing
{
    // "Called Shot (Ranger)", the form autocomplete uses when a move name could belong to more than one class
    [GeneratedRegex(@"^(?<name>.+?)\s*\((?<class>[^()]+)\)\s*$")]
    private static partial Regex QualifiedName();

    /// <summary>
    /// The moves on a sheet that other classes can take: starting and advanced moves, but not choices, death moves,
    /// or moves that themselves grant other classes' moves.
    /// </summary>
    public static IEnumerable<ClassMove> TakeableMoves(ClassSheet sheet) =>
        sheet.StartingMoves.Concat(sheet.AdvancedMoves).Where(m => m.Multiclass is null);

    public static string Qualify(ForeignMove move) => $"{move.Move.Name} ({move.Sheet.Type})";

    /// <summary>
    /// The character's moves that let them take moves from other classes.
    /// </summary>
    public static IReadOnlyList<ClassMove> Grants(ClassSheet own, IReadOnlyCollection<string> ownedMoves) =>
        own.AllMoves
            .Where(m => m.Multiclass is not null && ownedMoves.Contains(m.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Moves from other sheets matching a name, optionally written as "Name (Class)".
    /// </summary>
    public static IReadOnlyList<ForeignMove> Find(string input, ClassSheet own)
    {
        var name = input.Trim();
        ClassType? fromClass = null;

        if (QualifiedName().Match(name) is { Success: true } match
            && Enum.TryParse<ClassType>(match.Groups["class"].Value.Replace(" ", string.Empty), ignoreCase: true, out var parsed))
        {
            name = match.Groups["name"].Value;
            fromClass = parsed;
        }

        return ClassSheetLibrary.All
            .Where(s => s.Type != own.Type && (fromClass is null || s.Type == fromClass))
            .SelectMany(s => TakeableMoves(s).Where(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(m => new ForeignMove(s, m)))
            .ToList();
    }

    /// <summary>
    /// The other classes' moves the character has learned.
    /// </summary>
    public static IReadOnlyList<ForeignMove> KnownForeignMoves(Character character, ClassSheet own)
    {
        var known = new List<ForeignMove>();

        foreach (var ability in character.Abilities?.OrderBy(a => a.ID) ?? Enumerable.Empty<Ability>())
        {
            if (own.FindMove(ability.Name) is not null)
                continue;

            var matches = Find(ability.Name, own);
            if (matches.Count == 0)
                continue;

            // Learned moves note which sheet they came from, which settles names shared by two classes
            known.Add(matches.FirstOrDefault(m => ability.Description?.Contains(SourceNote(m.Sheet)) == true) ?? matches[0]);
        }

        return known;
    }

    /// <summary>
    /// Added to a learned move's description to record where it came from.
    /// </summary>
    public static string SourceNote(ClassSheet sheet) => $"*From the {sheet.Type} sheet.*";

    /// <returns>Why the character can't learn the move right now, or null if they can.</returns>
    public static string? WhyCannotLearn(Character character, ClassSheet own, ForeignMove candidate)
    {
        var owned = ClassSheetLibrary.KnownMoves(character);
        var move = candidate.Move;

        if (owned.Contains(move.Name, StringComparer.OrdinalIgnoreCase))
            return $"You already have {move.Name}.";

        var grants = Grants(own, owned);
        if (grants.Count == 0)
            return $"{move.Name} is a {candidate.Sheet.Type} move. You need a move like Special Trick or a Dabbler move before you can take moves from other classes.";

        if (move.Requires is not null && !move.RequiredMoves.Any(r => owned.Contains(r, StringComparer.OrdinalIgnoreCase)))
            return $"{move.Name} requires {move.Requires}.";

        if (!grants.Any(g => Allows(g.Multiclass!, own, candidate, character.Level)))
        {
            return $"None of your moves let you take {move.Name} from the {candidate.Sheet.Type} at level {character.Level}. " +
                   $"You have {string.Join(", ", grants.Select(g => g.Name))}.";
        }

        var taken = KnownForeignMoves(character, own).Append(candidate).ToList();
        if (!CanCover(grants, taken, own, character.Level))
            return $"You've already used {string.Join(", ", grants.Select(g => g.Name))} on other classes' moves.";

        return null;
    }

    public static bool Allows(MulticlassGrant grant, ClassSheet own, ForeignMove candidate, int level)
    {
        var effectiveLevel = grant.LevelOneLower ? level - 1 : level;

        return candidate.Sheet.Type != own.Type
               && (grant.From is null || grant.From.Contains(candidate.Sheet.Type))
               && (grant.OnlyMoves.Count == 0 || grant.OnlyMoves.Contains(candidate.Move.Name, StringComparer.OrdinalIgnoreCase))
               && !grant.ExcludedMoves.Contains(candidate.Move.Name, StringComparer.OrdinalIgnoreCase)
               && candidate.Move.MinLevel <= effectiveLevel;
    }

    /// <summary>
    /// Whether each foreign move can be paired with its own grant (one move per grant).
    /// </summary>
    private static bool CanCover(IReadOnlyList<ClassMove> grants, IReadOnlyList<ForeignMove> moves, ClassSheet own, int level)
    {
        if (moves.Count > grants.Count)
            return false;

        var grantTaken = new int?[grants.Count];

        // Simple augmenting-path matching; there are only ever a handful of each
        bool TryAssign(int moveIndex, bool[] visited)
        {
            for (var g = 0; g < grants.Count; g++)
            {
                if (visited[g] || !Allows(grants[g].Multiclass!, own, moves[moveIndex], level))
                    continue;

                visited[g] = true;
                if (grantTaken[g] is not { } holder || TryAssign(holder, visited))
                {
                    grantTaken[g] = moveIndex;
                    return true;
                }
            }

            return false;
        }

        return Enumerable.Range(0, moves.Count).All(i => TryAssign(i, new bool[grants.Count]));
    }
}
