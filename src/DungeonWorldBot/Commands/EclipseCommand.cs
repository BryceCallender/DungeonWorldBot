using System.ComponentModel;
using DungeonWorldBot.ClassSheets.DungeonWorld;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using DungeonWorldBot.Subclasses;
using DungeonWorldBot.Subclasses.Homebrew;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

public enum AspectChange
{
    [Description("At dawn or dusk")]
    DawnOrDusk,

    [Description("Spend 1 Shapeshift Hold")]
    SpendHold,

    [Description("After succeeding on Defy Danger (WIS) under intense stress")]
    DefyDanger,
}

/// <summary>
/// The Circle of the Eclipse's aspect. Its rolled moves are rolled with /move class.
/// </summary>
[Group("eclipse")]
public class EclipseCommand : CommandGroup
{
    private const string ShapeshiftHold = "Shapeshift Hold";

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IAbilityService _abilityService;
    private readonly IResourceService _resourceService;

    public EclipseCommand(
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

    [Command("aspect")]
    [Description("Attune yourself to the Sunward or Moonward aspect")]
    public async Task<IResult> AspectAsync(
        [Description("Sunward: healing and radiance. Moonward: phasing and shadow.")]
        EclipseAspect aspect,
        [Description("How you're changing aspect. Not needed for your first one.")]
        AspectChange how = AspectChange.DawnOrDusk)
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

        var character = await _characterService.GetCharacterFromUserAsync(userId);
        if (character is null)
            return await ReplyWithErrorAsync("No character exists. Try making one with /character create");

        if (SubclassLibrary.Of(character) != CircleOfTheEclipse.Subclass)
            return await ReplyWithErrorAsync($"{character.Name} isn't in the {CircleOfTheEclipse.Name}. See /subclass info {CircleOfTheEclipse.Name}");

        var current = CircleOfTheEclipse.AspectOf(character);
        if (current == aspect)
            return await ReplyWithErrorAsync($"{character.Name} is already {aspect}.");

        var fields = new List<IEmbedField>();

        // Your first aspect is simply chosen. Changing it later has a cost.
        if (current is not null)
        {
            if (how == AspectChange.SpendHold)
            {
                var hold = Druid.Sheet.FindResource(ShapeshiftHold)!;
                var held = _resourceService.GetValue(character, hold);
                if (held < 1)
                    return await ReplyWithErrorAsync($"{character.Name} has no {ShapeshiftHold} to spend.");

                await _resourceService.SetValueAsync(character, hold, held - 1);
                fields.Add(new EmbedField(ShapeshiftHold, _resourceService.Format(character, hold), IsInline: true));
            }

            fields.Insert(0, new EmbedField("Changed", how switch
            {
                AspectChange.DawnOrDusk => "At dawn or dusk",
                AspectChange.SpendHold => "By spending 1 Shapeshift Hold",
                _ => "Under intense stress, with Defy Danger (WIS)",
            }, IsInline: true));
        }

        if (_abilityService.FindAbility(character, CircleOfTheEclipse.CurrentAspect) is { } ability)
            await _abilityService.SetDescriptionAsync(ability, aspect.ToString());
        else
            await _abilityService.AddAbilityAsync(character, CircleOfTheEclipse.CurrentAspect, aspect.ToString(), null);

        var moves = CircleOfTheEclipse.AspectMoves(aspect, character.Level).ToList();
        if (moves.Count > 0)
            fields.Add(new EmbedField("Aspect moves", string.Join(", ", moves)));

        var title = current is null
            ? $"{CircleOfTheEclipse.Icon(aspect)} {character.Name} attunes to the {aspect} aspect"
            : $"{CircleOfTheEclipse.Icon(aspect)} {character.Name} shifts from {current} to {aspect}";

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: title,
                Description: current is null ? "*The sun and moon find their balance in you.*" : "*An eclipse flashes in their eyes.*",
                Fields: fields,
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
