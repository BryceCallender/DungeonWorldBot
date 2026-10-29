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
/// Suggests class sheet names as the player types, from their own class when they have a character with a class
/// sheet, and from every class otherwise.
/// </summary>
public abstract class ClassSheetAutocomplete(
    ContextInjectionService? contextInjection,
    ICharacterService? characterService)
    : IAutocompleteProvider
{
    // Discord shows at most 25 suggestions, each up to 100 characters
    private const int MaxSuggestions = 25;
    private const int MaxLength = 100;

    private readonly ContextInjectionService? _contextInjection = contextInjection;
    private readonly ICharacterService? _characterService = characterService;

    public abstract string Identity { get; }

    /// <summary>
    /// Suggestions for a player whose class has a sheet.
    /// </summary>
    protected abstract IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet);

    /// <summary>
    /// Suggestions when there's no character to go on, or the command isn't about the player's own class.
    /// </summary>
    protected abstract IEnumerable<Suggestion> AllClassCandidates();

    protected virtual bool OwnClassOnly => true;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var input = userInput.Trim();
        var (character, sheet) = await OwnSheetAsync();
        var candidates = character is not null && sheet is not null
            ? OwnClassCandidates(character, sheet)
            : AllClassCandidates();

        return candidates
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

    private async Task<(Character?, ClassSheet?)> OwnSheetAsync()
    {
        if (OwnClassOnly
            && _characterService is not null
            && _contextInjection?.Context is { } context
            && context.TryGetUserID(out var userId)
            && await _characterService.GetCharacterFromUserAsync(userId) is { } character
            && ClassSheetLibrary.For(character.Class.Type) is { } sheet)
        {
            return (character, sheet);
        }

        return (null, null);
    }

    private static string Truncate(string value) => value.Length <= MaxLength ? value : value[..MaxLength];
}

/// <summary>
/// Moves the player can learn: their own sheet's, plus other classes' when a move like Special Trick allows it.
/// </summary>
public class ClassMoveAutocomplete(
    ContextInjectionService? contextInjection = null,
    ICharacterService? characterService = null)
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

    protected override IEnumerable<Suggestion> AllClassCandidates() => Labeled(ClassSheetLibrary.All, s => s.AllMoves.Select(m => m.Name));
}

/// <summary>
/// Moves from every class sheet and subclass, for looking things up.
/// </summary>
public class AnyClassMoveAutocomplete(
    ContextInjectionService? contextInjection = null,
    ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "any-class-move";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) => AllClassCandidates();

    protected override IEnumerable<Suggestion> AllClassCandidates()
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
    ContextInjectionService? contextInjection = null,
    ICharacterService? characterService = null)
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

    protected override IEnumerable<Suggestion> AllClassCandidates() =>
        Labeled(ClassSheetLibrary.All, s => s.AllMoves.Where(m => m.Roll is not null).Select(m => m.Name));
}

public class ClassResourceAutocomplete(
    ContextInjectionService? contextInjection = null,
    ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "class-resource";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> OwnClassCandidates(Character character, ClassSheet sheet) =>
        ClassSheetLibrary.ResourcesOf(character).Select(r => new Suggestion(r.Name, r.Name));

    protected override IEnumerable<Suggestion> AllClassCandidates() =>
        Labeled(ClassSheetLibrary.All, s => s.Resources.Select(r => r.Name))
            .Concat(ClassSheetLibrary.CommonResources.Select(r => new Suggestion(r.Name, r.Name)));
}
