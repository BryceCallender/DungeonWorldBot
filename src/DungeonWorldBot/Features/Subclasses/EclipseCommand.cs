using System.ComponentModel;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes.DungeonWorld;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Shapeshifting;
using DungeonWorldBot.Features.Subclasses.Homebrew;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Subclasses;

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
public class EclipseCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IAbilityService abilityService,
    IResourceService resourceService,
    ITransformationService transformationService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string ShapeshiftHold = "Shapeshift Hold";

    [Command("aspect")]
    [Description("Attune yourself to the Sunward or Moonward aspect")]
    public async Task<IResult> AspectAsync(
        [Description("Sunward: healing and radiance. Moonward: phasing and shadow.")]
        EclipseAspect aspect,
        [Description("How you're changing aspect. Not needed for your first one.")]
        AspectChange how = AspectChange.DawnOrDusk)
    {
        var userId = GetUserId();

        var character = await characterService.GetCharacterFromUserAsync(userId);
        if (character is null)
            return await ReplyWithErrorAsync("No character exists. Try making one with /character create");

        if (SubclassLibrary.Of(character) != CircleOfTheEclipse.Subclass)
            return await ReplyWithErrorAsync($"{character.Name} isn't in the {CircleOfTheEclipse.Name}. See /subclass info {CircleOfTheEclipse.Name}");

        var current = CircleOfTheEclipse.AspectOf(character);
        if (current == aspect)
            return await ReplyWithErrorAsync($"{character.Name} is already {aspect}.");

        List<IEmbedField> fields = [];

        // Your first aspect is simply chosen. Changing it later has a cost.
        if (current is not null)
        {
            if (how == AspectChange.SpendHold)
            {
                var hold = Druid.Sheet.FindResource(ShapeshiftHold)!;
                var held = resourceService.GetValue(character, hold);
                if (held < 1)
                    return await ReplyWithErrorAsync($"{character.Name} has no {ShapeshiftHold} to spend.");

                await resourceService.SetValueAsync(character, hold, held - 1);
                fields.Add(new EmbedField(ShapeshiftHold, resourceService.Format(character, hold), IsInline: true));

                if (held == 1 && ShapeshiftRules.FormName(character) is { } form)
                {
                    await transformationService.RevertAsync(character);
                    fields.Add(new EmbedField("Natural form", $"Out of hold, {character.Name} returns from {form} to their natural form."));
                }
            }

            fields.Insert(0, new EmbedField("Changed", how switch
            {
                AspectChange.DawnOrDusk => "At dawn or dusk",
                AspectChange.SpendHold => "By spending 1 Shapeshift Hold",
                _ => "Under intense stress, with Defy Danger (WIS)",
            }, IsInline: true));
        }

        if (abilityService.FindAbility(character, CircleOfTheEclipse.CurrentAspect) is { } ability)
            await abilityService.SetDescriptionAsync(ability, aspect.ToString());
        else
            await abilityService.AddAbilityAsync(character, CircleOfTheEclipse.CurrentAspect, aspect.ToString(), null);

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
}
