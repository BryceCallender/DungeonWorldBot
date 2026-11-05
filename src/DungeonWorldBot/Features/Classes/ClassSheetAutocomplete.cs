using System.Collections.Concurrent;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Subclasses;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.Features.Classes;

/// <summary>
/// A suggestion: the value sent to the command, and the label the player sees.
/// </summary>
public readonly record struct Suggestion(string Value, string Label);

/// <summary>
/// Suggestions as the player types, filtered to what they've typed and cut to what Discord shows.
/// </summary>
public abstract class SuggestionAutocomplete : IAutocompleteProvider
{
    // Discord shows at most 25 suggestions, each up to 100 characters
    private const int MaxSuggestions = 25;
    private const int MaxLength = 100;

    // Every suggestion comes from the sheets, which never change, so each kind is built once
    private static readonly ConcurrentDictionary<Type, IReadOnlyList<Suggestion>> AllSuggestions = new();

    public abstract string Identity { get; }

    /// <summary>
    /// Every suggestion there is, whoever's typing.
    /// </summary>
    protected abstract IEnumerable<Suggestion> AllCandidates();

    /// <summary>
    /// The suggestions for whoever's typing: every one, unless a kind narrows them down.
    /// </summary>
    protected virtual Task<IEnumerable<Suggestion>> CandidatesAsync() => Task.FromResult<IEnumerable<Suggestion>>(Every);

    protected IReadOnlyList<Suggestion> Every => AllSuggestions.GetOrAdd(GetType(), _ => AllCandidates().ToList());

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();
        return (await CandidatesAsync())
            .Where(c => c.Label.Contains(input, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(c => c.Value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => !c.Value.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Label)
            .Take(MaxSuggestions)
            .Select(c => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(Truncate(c.Label), Truncate(c.Value)))
            .ToList();
    }

    /// <summary>
    /// Every sheet's entries, labeled with their class. Names on more than one sheet send "Name (Class)" so the
    /// command can tell them apart.
    /// </summary>
    protected static IEnumerable<Suggestion> Labeled(IEnumerable<ClassSheet> sheets, Func<ClassSheet, IEnumerable<string>> names)
    {
        var entries = sheets.SelectMany(s => names(s).Select(n => (Sheet: s, Name: n))).ToList();
        var shared = entries.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return entries.Select(e =>
        {
            var label = $"{e.Name} ({e.Sheet.Type})";
            return new Suggestion(shared.Contains(e.Name) ? label : e.Name, label);
        });
    }

    private static string Truncate(string value) => value.Length <= MaxLength ? value : value[..MaxLength];
}

/// <summary>
/// Suggests class sheet entries as the player types, from their own class when they have a character with a class
/// sheet, and from every class otherwise.
/// </summary>
public abstract class ClassSheetAutocomplete(
    ContextInjectionService contextInjection,
    ICharacterService characterService)
    : SuggestionAutocomplete
{
    /// <summary>
    /// Suggestions for a player whose class has a sheet.
    /// </summary>
    protected abstract IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet);

    protected override async Task<IEnumerable<Suggestion>> CandidatesAsync() =>
        await OwnSheetAsync() is { } own ? OwnClassCandidates(own.Character, own.Sheet) : Every;

    private record OwnSheet(Character Character, ClassSheet Sheet);

    /// <returns>The player's character and their class's sheet, or null when there's no character with one.</returns>
    private async Task<OwnSheet?> OwnSheetAsync()
    {
        if (contextInjection.Context is { } context
            && context.TryGetUserID(out var userId)
            && await characterService.GetCharacterMovesAsync(userId) is { } character
            && ClassSheetLibrary.For(character.Class.Type) is { } sheet)
        {
            return new OwnSheet(character, sheet);
        }

        return null;
    }
}

/// <summary>
/// Moves the player can learn: their own sheet's, plus other classes' when a move like Special Trick allows it.
/// </summary>
public class ClassMoveAutocomplete(
    ContextInjectionService contextInjection,
    ICharacterService characterService)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "class-move";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet)
    {
        var own = sheet.AllMoves.Select(m => new Suggestion(m.Name, m.Name));

        var grants = Multiclassing.Grants(sheet, ClassSheetLibrary.KnownMoves(character));
        if (grants.Count == 0)
            return own;

        var foreign = ClassSheetLibrary.All
            .Where(s => s.Type != sheet.Type)
            .SelectMany(s => Multiclassing.TakeableMoves(s).Select(m => new ForeignMove(s, m)))
            .Where(f => grants.Any(g => Multiclassing.Allows(g.Multiclass!, sheet, f, character.Level)))
            .Select(f => new Suggestion(Multiclassing.Qualify(f), Multiclassing.Qualify(f)));

        return own.Concat(foreign);
    }

    protected override IEnumerable<Suggestion> AllCandidates() => Labeled(ClassSheetLibrary.All, s => s.AllMoves.Select(m => m.Name));
}

/// <summary>
/// Moves from every class sheet and subclass, for looking things up.
/// </summary>
public class AnyClassMoveAutocomplete : SuggestionAutocomplete
{
    public const string ID = "any-class-move";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates()
    {
        var classMoves = Labeled(ClassSheetLibrary.All, s => s.AllMoves.Select(m => m.Name)).ToList();
        var taken = ClassSheetLibrary.All.SelectMany(s => s.AllMoves.Select(m => m.Name))
            .Concat(SubclassLibrary.All.SelectMany(s => s.Moves.Where(m => !m.Hidden).Select(m => m.Move.Name)))
            .GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Subclass moves send "Name (Subclass)" when the name is also on a class sheet or another subclass
        var subclassMoves = SubclassLibrary.All.SelectMany(s => s.Moves.Where(m => !m.Hidden).Select(m =>
        {
            var label = $"{m.Move.Name} ({s.Name})";
            return new Suggestion(taken.Contains(m.Move.Name) ? label : m.Move.Name, label);
        }));

        return classMoves.Concat(subclassMoves);
    }
}

/// <summary>
/// Rolled moves the player can make: their own class's, their subclass's, plus any rolled moves they learned from other classes.
/// </summary>
public class RolledClassMoveAutocomplete(
    ContextInjectionService contextInjection,
    ICharacterService characterService)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "rolled-class-move";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        sheet.AllMoves.Where(m => m.Roll is not null).Select(m => new Suggestion(m.Name, m.Name))
            .Concat((SubclassLibrary.Of(character)?.MovesAt(character.Level) ?? [])
                .Where(m => m.Roll is not null)
                .Select(m => new Suggestion(m.Name, m.Name)))
            .Concat(Multiclassing.KnownForeignMoves(character, sheet)
                .Where(f => f.Move.Roll is not null)
                .Select(f => new Suggestion(f.Move.Name, Multiclassing.Qualify(f))));

    protected override IEnumerable<Suggestion> AllCandidates() =>
        Labeled(ClassSheetLibrary.All, s => s.AllMoves.Where(m => m.Roll is not null).Select(m => m.Name));
}

public class ClassResourceAutocomplete(
    ContextInjectionService contextInjection,
    ICharacterService characterService)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "class-resource";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        ClassSheetLibrary.ResourcesOf(character).Select(r => new Suggestion(r.Name, r.Name));

    protected override IEnumerable<Suggestion> AllCandidates() =>
        Labeled(ClassSheetLibrary.All, s => s.Resources.Select(r => r.Name))
            .Concat(ClassSheetLibrary.CommonResources.Select(r => new Suggestion(r.Name, r.Name)));
}
