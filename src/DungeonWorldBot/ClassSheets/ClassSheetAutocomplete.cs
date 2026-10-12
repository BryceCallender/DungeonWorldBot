using DungeonWorldBot.Services;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;

namespace DungeonWorldBot.ClassSheets;

/// <summary>
/// Suggests class sheet names as the player types, from their own class when they have a character with a class
/// sheet, and from every class otherwise.
/// </summary>
public abstract class ClassSheetAutocomplete : IAutocompleteProvider
{
    // Discord shows at most 25 suggestions, each up to 100 characters
    private const int MaxSuggestions = 25;
    private const int MaxLength = 100;

    private readonly ContextInjectionService? _contextInjection;
    private readonly ICharacterService? _characterService;

    protected ClassSheetAutocomplete(ContextInjectionService? contextInjection, ICharacterService? characterService)
    {
        _contextInjection = contextInjection;
        _characterService = characterService;
    }

    public abstract string Identity { get; }

    /// <summary>
    /// Whether suggestions should stick to the player's own class.
    /// </summary>
    protected virtual bool OwnClassOnly => true;

    protected abstract IEnumerable<string> Candidates(ClassSheet sheet);

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct = default)
    {
        var sheets = await SheetsToSuggestFromAsync();
        var input = userInput.Trim();
        var showClass = sheets.Count > 1;

        return sheets
            .SelectMany(s => Candidates(s).Select(name => (Name: name, Label: showClass ? $"{name} ({s.Type})" : name)))
            .Where(c => c.Label.Contains(input, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => !c.Name.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.Label)
            .Take(MaxSuggestions)
            .Select(c => (IApplicationCommandOptionChoice)new ApplicationCommandOptionChoice(Truncate(c.Label), Truncate(c.Name)))
            .ToList();
    }

    private async Task<IReadOnlyList<ClassSheet>> SheetsToSuggestFromAsync()
    {
        if (OwnClassOnly
            && _characterService is not null
            && _contextInjection?.Context is { } context
            && context.TryGetUserID(out var userId)
            && await _characterService.GetCharacterFromUserAsync(userId) is { } character
            && ClassSheetLibrary.For(character.Class.Type) is { } sheet)
        {
            return [sheet];
        }

        return ClassSheetLibrary.All;
    }

    private static string Truncate(string value) => value.Length <= MaxLength ? value : value[..MaxLength];
}

/// <summary>
/// Moves from the player's own class sheet.
/// </summary>
public sealed class ClassMoveAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "class-move";

    public override string Identity => ID;

    protected override IEnumerable<string> Candidates(ClassSheet sheet) => sheet.AllMoves.Select(m => m.Name);
}

/// <summary>
/// Moves from every class sheet, for looking things up.
/// </summary>
public sealed class AnyClassMoveAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "any-class-move";

    public override string Identity => ID;

    protected override bool OwnClassOnly => false;

    protected override IEnumerable<string> Candidates(ClassSheet sheet) => sheet.AllMoves.Select(m => m.Name);
}

public sealed class RolledClassMoveAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "rolled-class-move";

    public override string Identity => ID;

    protected override IEnumerable<string> Candidates(ClassSheet sheet) => sheet.AllMoves.Where(m => m.Roll is not null).Select(m => m.Name);
}

public sealed class ClassResourceAutocomplete(ContextInjectionService? contextInjection = null, ICharacterService? characterService = null)
    : ClassSheetAutocomplete(contextInjection, characterService)
{
    public const string ID = "class-resource";

    public override string Identity => ID;

    protected override IEnumerable<string> Candidates(ClassSheet sheet) => sheet.Resources.Select(r => r.Name);
}
