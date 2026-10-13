using System.ComponentModel;
using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Services;
using DungeonWorldBot.Subclasses;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Pagination.Extensions;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("class")]
public class ClassCommand : CommandGroup
{
    private const string NoCharacterMessage = "No character exists. Try making one with /character create";

    // Move summaries are long, so keep pages small enough to stay under Discord's embed size limit
    private const int MovesPerPage = 6;

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IAbilityService _abilityService;
    private readonly IResourceService _resourceService;

    public ClassCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IAbilityService abilityService,
        IResourceService resourceService)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _abilityService = abilityService;
        _resourceService = resourceService;
    }

    [Command("sheet")]
    [Description("Show a character's class sheet: resources, choices still to make, and advanced moves")]
    public async Task<IResult> ShowSheetAsync(
        [Description("The user to see the class sheet of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await _characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? NoCharacterMessage
                : $"{user.Username} does not have a character.");
        }

        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return await ReplyWithNoSheetAsync(character);

        var known = ClassSheetLibrary.KnownMoves(character);
        var fields = new List<IEmbedField>();

        var resources = sheet.ResourcesFor(known).ToList();
        if (resources.Count > 0)
        {
            fields.Add(new EmbedField("Resources", string.Join('\n', resources.Select(r => $"**{r.Name}:** {FormatResource(character, r)}"))));
        }

        var missing = sheet.GrantedMoves.Where(m => !known.Contains(m.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            fields.Add(new EmbedField("Missing Starting Moves", $"{string.Join(", ", missing.Select(m => m.Name))}\nAdd them all with /class setup"));
        }

        var choices = sheet.Features
            .Select(f => (Feature: f, Taken: f.Options.Where(o => known.Contains(o.Name, StringComparer.OrdinalIgnoreCase)).ToList()))
            .Select(f => $"**{f.Feature.Name}:** {(f.Taken.Count > 0 ? string.Join(", ", f.Taken.Select(o => o.Name)) : "*not chosen*")}"
                         + (f.Taken.Count < f.Feature.AllowedPicks(known) ? $" *({f.Feature.AllowedPicks(known) - f.Taken.Count} to pick with /class learn)*" : string.Empty));
        if (sheet.Features.Count > 0)
        {
            fields.Add(new EmbedField("Choices", string.Join('\n', choices)));
        }

        if (SubclassLibrary.Of(character) is { } subclass)
        {
            var subclassMoves = subclass.MovesAt(character.Level).Select(m => m.Name).ToList();
            fields.Add(new EmbedField($"Subclass: {subclass.Name}",
                (subclassMoves.Count > 0 ? string.Join(", ", subclassMoves) : "No moves yet") + "\nSee /subclass info"));
        }

        var grants = Multiclassing.Grants(sheet, known);
        if (grants.Count > 0)
        {
            var foreign = Multiclassing.KnownForeignMoves(character, sheet);
            var free = grants.Count - foreign.Count;
            fields.Add(new EmbedField($"Other Classes' Moves ({foreign.Count}/{grants.Count})",
                (foreign.Count > 0 ? string.Join(", ", foreign.Select(Multiclassing.Qualify)) : "None yet") +
                (free > 0 ? $"\n{free} to pick with /class learn (from {string.Join(", ", grants.Select(g => g.Name))})" : string.Empty)));
        }

        var advanced = sheet.AdvancedMoves.Where(m => known.Contains(m.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        var advancesTaken = sheet.AdvancesTaken(known);
        var picksLeft = Math.Max(0, character.Level - 1 - advancesTaken);
        fields.Add(new EmbedField($"Advanced Moves ({advancesTaken}/{character.Level - 1})",
            (advanced.Count > 0 ? string.Join(", ", advanced.Select(m => m.Name)) : "None yet") +
            (picksLeft > 0 ? $"\n{picksLeft} to pick. See /class moves" : string.Empty)));

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}: The {character.Class.Type}",
                Thumbnail: ImageHelper.ClassThumbnail(character.Class.Type),
                Description: $"{sheet.Source} class sheet, level {character.Level}. Full move text is on /ability list.",
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("setup")]
    [Description("Add any starting moves from your class sheet that your character is missing")]
    public async Task<IResult> SetupAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return await ReplyWithNoSheetAsync(character);

        var known = ClassSheetLibrary.KnownMoves(character);
        var added = new List<string>();
        foreach (var move in sheet.GrantedMoves.Where(m => !known.Contains(m.Name, StringComparer.OrdinalIgnoreCase)))
        {
            await _abilityService.AddAbilityAsync(character, move.Name, move.Summary, move.Uses);
            added.Add(move.Name);
        }

        known = ClassSheetLibrary.KnownMoves(character);
        var unchosen = sheet.Features
            .Where(f => !f.Options.Any(o => known.Contains(o.Name, StringComparer.OrdinalIgnoreCase)))
            .Select(f => $"**{f.Name}:** {string.Join(", ", f.Options.Select(o => o.Name))}")
            .ToList();

        var description = added.Count > 0
            ? $"Added {string.Join(", ", added)}."
            : "You already have every starting move.";
        if (unchosen.Count > 0)
            description += $"\n\nYou still need to choose with /class learn:\n{string.Join('\n', unchosen)}";

        return await ReplyAsync("Class Setup", description);
    }

    [Command("moves")]
    [Description("List the advanced moves you can choose from at your level")]
    public async Task<IResult> ListMovesAsync()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

        var character = await _characterService.GetCharacterFromUserAsync(userId);
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return await ReplyWithNoSheetAsync(character);

        var known = ClassSheetLibrary.KnownMoves(character);
        var available = sheet.AvailableAdvancedMoves(character.Level, known).ToList();
        if (available.Count == 0)
        {
            return await ReplyAsync("Advanced Moves", character.Level < 2
                ? "Advanced moves open up at level 2. Use /character level-up when you get there."
                : "There are no advanced moves left for you to pick at this level.");
        }

        var picksLeft = Math.Max(0, character.Level - 1 - sheet.AdvancesTaken(known));
        var header = picksLeft > 0
            ? $"You can pick {picksLeft} more. Learn one with /class learn"
            : "You've used all your picks for this level, but here's what's available when you level up.";

        var pages = available
            .Chunk(MovesPerPage)
            .Select(page => new Embed(
                Title: $"{character.Class.Type} Advanced Moves (level {character.Level})",
                Description: header,
                Colour: _feedbackService.Theme.Primary,
                Fields: page.Select(m => (IEmbedField)new EmbedField(MoveHeading(m), Truncate(m.Summary, 1024))).ToList()))
            .ToList();

        return await _feedbackService.SendContextualPaginatedMessageAsync(userId, pages);
    }

    [Command("learn")]
    [Description("Learn a move from your class sheet: an advanced move, a class choice, or a Shaman spirit totem")]
    public async Task<IResult> LearnAsync(
        [Description("The move to learn")]
        [AutocompleteProvider(ClassMoveAutocomplete.ID)]
        string move)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoCharacterMessage);

        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return await ReplyWithNoSheetAsync(character);

        var classMove = sheet.FindMove(move);
        if (classMove is null)
            return await LearnForeignMoveAsync(character, sheet, move);

        if (sheet.WhyCannotLearn(classMove, character.Level, ClassSheetLibrary.KnownMoves(character)) is { } problem)
            return await ReplyWithErrorAsync(problem);

        await _abilityService.AddAbilityAsync(character, classMove.Name, classMove.Summary, classMove.Uses);

        var description = $"{character.Name} learned **{classMove.Name}**\n\n{classMove.Summary}";
        if (classMove.Multiclass is not null)
            description += "\n\nPick the other class's move with /class learn";
        if (classMove.Replaces is not null && _abilityService.FindAbility(character, classMove.Replaces) is { } replaced)
        {
            await _abilityService.RemoveAbilityAsync(character, replaced);
            description += $"\n\n*Replaces {replaced.Name}.*";
        }

        if (classMove.Armor != 0)
        {
            await _characterService.AdjustArmorAsync(character, classMove.Armor);
            description += $"\n\nArmor is now {character.ArmorRating}.";
        }

        if (classMove.DamageDie is not null)
        {
            await _characterService.UpdateCharacterDamageModifierAsync(character, classMove.DamageDie);
            description += $"\n\nYour damage is now {classMove.DamageDie}.";
        }

        if (classMove.Roll is not null)
            description += $"\n\nRoll it with /move class";

        return await ReplyAsync("Move Learned!", Truncate(description, 4096));
    }

    [Command("lookup")]
    [Description("Read any move from a class sheet")]
    public async Task<IResult> LookupAsync(
        [Description("The move to look up")]
        [AutocompleteProvider(AnyClassMoveAutocomplete.ID)]
        string move)
    {
        var found = ClassSheetLibrary.All
            .Select(s => (Sheet: s, Move: s.FindMove(move)))
            .FirstOrDefault(m => m.Move is not null);

        // Autocomplete sends "Name (Class)" when a name is on more than one sheet
        if (found.Move is null && ClassSheetLibrary.All.SelectMany(s => s.AllMoves.Select(m => (Sheet: s, Move: (ClassMove?)m)))
                .FirstOrDefault(m => $"{m.Move!.Name} ({m.Sheet.Type})".Equals(move.Trim(), StringComparison.OrdinalIgnoreCase)) is { Move: not null } qualified)
        {
            found = qualified;
        }

        if (found.Move is null)
            return await ReplyWithErrorAsync($"No class sheet has a move called {move}.");

        return await ReplyAsync(
            $"{found.Move.Name} ({found.Sheet.Type}{TierLabel(found.Move)})",
            Truncate(found.Move.Summary, 4096));
    }

    private async Task<IResult> LearnForeignMoveAsync(Character character, ClassSheet sheet, string move)
    {
        var matches = Multiclassing.Find(move, sheet);
        if (matches.Count == 0)
            return await ReplyWithErrorAsync($"No class sheet has a move called {move} that another class can take.");

        if (matches.Count > 1)
        {
            return await ReplyWithErrorAsync(
                $"More than one class has {matches[0].Move.Name}. Say which one, e.g. {string.Join(" or ", matches.Select(Multiclassing.Qualify))}");
        }

        var foreign = matches[0];
        if (Multiclassing.WhyCannotLearn(character, sheet, foreign) is { } problem)
            return await ReplyWithErrorAsync(problem);

        var summary = $"{foreign.Move.Summary}\n{Multiclassing.SourceNote(foreign.Sheet)}";
        await _abilityService.AddAbilityAsync(character, foreign.Move.Name, summary, foreign.Move.Uses);

        var description = $"{character.Name} learned **{foreign.Move.Name}** from the {foreign.Sheet.Type} sheet\n\n{foreign.Move.Summary}";
        if (foreign.Move.Roll is not null)
            description += "\n\nRoll it with /move class";

        return await ReplyAsync("Move Learned!", Truncate(description, 4096));
    }

    private string FormatResource(Character character, ClassResource resource) => _resourceService.Format(character, resource);

    private static string MoveHeading(ClassMove move)
    {
        var heading = move.Name + TierLabel(move);
        if (move.Requires is not null)
            heading += $" · requires {move.Requires}";
        if (move.Replaces is not null)
            heading += $" · replaces {move.Replaces}";
        return Truncate(heading, 256);
    }

    private static string TierLabel(ClassMove move) => move.Tier switch
    {
        MoveTier.Level2To5 => ", levels 2-5",
        MoveTier.Level6To10 => ", levels 6-10",
        _ => string.Empty,
    };

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";

    private async Task<Character?> GetOwnCharacterAsync()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

        return await _characterService.GetCharacterFromUserAsync(userId);
    }

    private Task<Result> ReplyWithNoSheetAsync(Character character) =>
        ReplyWithErrorAsync($"The {character.Class.Type} doesn't have a class sheet in the bot yet. You can still track moves with /ability add");

    private async Task<IResult> ReplyAsync(string title, string description)
    {
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: title,
                Description: description,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    private async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync(error, ct: CancellationToken);
    }
}
