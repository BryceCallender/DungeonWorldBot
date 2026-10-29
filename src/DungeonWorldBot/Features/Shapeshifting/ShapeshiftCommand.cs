using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Shapeshifting;

/// <summary>
/// The forms a shapeshifter knows, the form they're in, and the hold they spend on its moves.
/// </summary>
[Group("shapeshift")]
public class ShapeshiftCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    ITransformationService transformationService,
    IResourceService resourceService,
    IDiceRoller diceRoller,
    IRollStatsService rollStatsService,
    IDamageService damageService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string BonusDescription = "Any +forward, ongoing bonus, or penalty to add to the roll";
    private const string FormDescription = "One of the forms you know";

    [Command("learn")]
    [Description("Add a form you can take: a species from your land, or one whose essence you've studied")]
    public async Task<IResult> LearnAsync(
        [Description("The form, e.g. Bear, Hawk or Giant Spider")]
        string name,
        [Description("Its natural abilities and weaknesses")]
        string? description = null,
        [Description("The moves the GM gives the form, which cost 1 hold each")]
        string? moves = null)
    {
        var (character, error) = await GetShapeshifterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (ShapeshiftRules.Find(character, name) is { } existing)
            return await ReplyWithErrorAsync($"{character.Name} already knows the {existing.Name} form. Change it with /shapeshift edit");

        var form = await transformationService.LearnAsync(character, name, description, moves);

        return await ReplyAsync($"🌿 {character.Name} learns the {form.Name} form",
            $"*The spirit of the {form.Name} lends its shape...*\n\n{ShapeshiftRules.Describe(form)}");
    }

    [Command("edit")]
    [Description("Change a form's description or the moves the GM gave it")]
    public async Task<IResult> EditAsync(
        [Description(FormDescription)]
        [AutocompleteProvider(TransformationAutocomplete.ID)]
        string form,
        [Description("Its natural abilities and weaknesses. Leave blank to keep it as it is")]
        string? description = null,
        [Description("The moves the GM gives the form. Leave blank to keep them as they are")]
        string? moves = null)
    {
        var (character, error) = await GetShapeshifterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (ShapeshiftRules.Find(character, form) is not { } known)
            return await ReplyWithErrorAsync(UnknownForm(character, form));

        if (description is null && moves is null)
            return await ReplyWithErrorAsync("Give a new description or moves for the form.");

        await transformationService.UpdateAsync(known, description, moves);

        return await ReplyAsync($"🌿 {character.Name}'s {known.Name} form", ShapeshiftRules.Describe(known));
    }

    [Command("forget")]
    [Description("Remove a form you can no longer take")]
    public async Task<IResult> ForgetAsync(
        [Description(FormDescription)]
        [AutocompleteProvider(TransformationAutocomplete.ID)]
        string form)
    {
        var (character, error) = await GetShapeshifterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (ShapeshiftRules.Find(character, form) is not { } known)
            return await ReplyWithErrorAsync(UnknownForm(character, form));

        if (known.IsActive)
            return await ReplyWithErrorAsync($"{character.Name} is in the {known.Name} form. Return to your natural form with /shapeshift revert first.");

        await transformationService.ForgetAsync(character, known);

        return await ReplyAsync("Form Forgotten", $"{character.Name} can no longer take the {known.Name} form.");
    }

    [Command("forms")]
    [Description("See the forms a character knows and the one they're in")]
    public async Task<IResult> FormsAsync(
        [Description("The user to see the forms of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? "No character exists. Try making one with /character create"
                : $"{user.Username} does not have a character.");
        }

        if (!ShapeshiftRules.CanShapeshift(character))
            return await ReplyWithErrorAsync($"{character.Name} can't shapeshift.");

        var forms = character.Transformations?.OrderByDescending(t => t.IsActive).ThenBy(t => t.Name).ToList() ?? [];

        // Embeds hold at most 25 fields
        var fields = forms.Take(24)
            .Select(f => (IEmbedField)new EmbedField(f.IsActive ? $"🐾 {f.Name} (current)" : f.Name, Truncate(ShapeshiftRules.Describe(f))))
            .ToList();
        if (forms.Count > fields.Count)
            fields.Add(new EmbedField("...", $"and {forms.Count - fields.Count} more"));

        var description = ShapeshiftRules.Status(character, resourceService.Format(character, ShapeshiftRules.Hold));
        if (forms.Count == 0)
            description += "\n\nNo forms yet. Add the animals of your land and the essences you've studied with /shapeshift learn";

        return await ReplyAsync($"{character.Name}'s Forms", description, fields);
    }

    [Command("into")]
    [Description("Call on the spirits to change your shape: roll Shapeshifter (+WIS) and gain hold")]
    public async Task<IResult> IntoAsync(
        [Description(FormDescription)]
        [AutocompleteProvider(TransformationAutocomplete.ID)]
        string form,
        [Description(BonusDescription)]
        int bonus = 0,
        [Description("Chimera: a second form to merge with the first")]
        [AutocompleteProvider(TransformationAutocomplete.ID)]
        [Option("merge-with")]
        string? mergeWith = null,
        [Description("Chimera: a third form to merge with the others")]
        [AutocompleteProvider(TransformationAutocomplete.ID)]
        [Option("and-with")]
        string? andWith = null)
    {
        var (character, error) = await GetShapeshifterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        var names = new[] { form, mergeWith, andWith }.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
        if (names.Count > ShapeshiftRules.MaxShapes(character))
            return await ReplyWithErrorAsync($"{character.Name} needs {ShapeshiftRules.Chimera} to merge shapes.");

        List<Transformation> forms = [];
        foreach (var name in names)
        {
            if (ShapeshiftRules.Find(character, name) is not { } known)
                return await ReplyWithErrorAsync(UnknownForm(character, name));
            if (!forms.Contains(known))
                forms.Add(known);
        }

        var previous = ShapeshiftRules.FormName(character);

        var definition = ShapeshiftRules.Move.Roll!;
        var (roll, rolledWith) = await RollStatAsync(character, StatType.Wis, bonus);

        var hold = ShapeshiftRules.HoldFor(roll.Outcome);
        List<string> holdParts = [hold.ToString()];
        if (ShapeshiftRules.Knows(character, ShapeshiftRules.EmbracingNoForm))
        {
            var extra = diceRoller.Roll("1d4").Value;
            hold += extra;
            holdParts.Add($"{ShapeshiftRules.EmbracingNoForm} 1d4 → {extra}");
        }

        await transformationService.ShiftAsync(character, forms, hold);

        var formName = ShapeshiftRules.FormName(character)!;
        List<IEmbedField> fields =
        [
            new EmbedField("Rolled with", rolledWith, IsInline: true),
            new EmbedField(ShapeshiftRules.HoldName, holdParts.Count > 1 ? $"{string.Join(" + ", holdParts)} = **{hold}**" : $"**{hold}**", IsInline: true),
            new EmbedField(MoveRoller.Describe(roll.Outcome), definition.Describe(roll.Outcome)),
        ];

        fields.AddRange(forms.Select(f => new EmbedField($"🐾 {f.Name}", Truncate(ShapeshiftRules.Describe(f)))));

        if (ShapeshiftRules.Reminders(character).ToList() is { Count: > 0 } reminders)
            fields.Add(new EmbedField("When you shapeshift", string.Join('\n', reminders)));

        fields.Add(new EmbedField("Next", "Spend hold on the form's moves with /shapeshift move. Return to your natural form with /shapeshift revert"));

        var title = previous is not null && previous != formName
            ? $"🐾 {character.Name}: {previous} → {formName}"
            : $"🐾 {character.Name} becomes {Article(formName)} {formName}";

        return await ReplyWithRollAsync(character, title, $"*{definition.Trigger}...*", roll, fields);
    }

    [Command("move")]
    [Description("Spend hold to make one of your form's moves")]
    public async Task<IResult> MoveAsync(
        [Description("What you do, e.g. Maul or Fly to the treetops")]
        string? what = null,
        [Description("How much hold to spend. Spending it all at once returns you to your natural form")]
        [MinValue(1)]
        int amount = 1)
    {
        var (character, error) = await GetShapeshifterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (ShapeshiftRules.FormName(character) is not { } formName)
            return await ReplyWithErrorAsync($"{character.Name} is in their natural form. Shapeshift with /shapeshift into");

        var held = resourceService.GetValue(character, ShapeshiftRules.Hold);
        if (held < amount)
            return await ReplyWithErrorAsync($"{character.Name} only has {held} {ShapeshiftRules.HoldName}.");

        var left = await resourceService.SetValueAsync(character, ShapeshiftRules.Hold, held - amount);

        var description = $"*The {formName} {(string.IsNullOrWhiteSpace(what) ? "makes its move" : what.Trim())}.*";
        List<IEmbedField> fields =
        [
            new EmbedField(ShapeshiftRules.HoldName, $"-{amount} ({resourceService.Format(character, ShapeshiftRules.Hold)})", IsInline: true),
        ];

        if (left == 0)
        {
            await transformationService.RevertAsync(character);
            fields.Add(new EmbedField("Natural form", $"Out of hold, {character.Name} returns to their natural form."));
        }

        return await ReplyAsync($"🐾 {character.Name}: {formName}", description, fields);
    }

    [Command("revert")]
    [Description("Return to your natural form, losing any hold left")]
    public async Task<IResult> RevertAsync(
        [Description("Shed: you're reverting to negate damage you just took")]
        bool shed = false)
    {
        var (character, error) = await GetShapeshifterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(error!);

        if (ShapeshiftRules.FormName(character) is not { } formName)
            return await ReplyWithErrorAsync($"{character.Name} is already in their natural form.");

        if (shed && !ShapeshiftRules.Knows(character, ShapeshiftRules.Shed))
            return await ReplyWithErrorAsync($"{character.Name} needs the {ShapeshiftRules.Shed} move to negate damage by reverting.");

        var lost = resourceService.GetValue(character, ShapeshiftRules.Hold);
        await transformationService.RevertAsync(character);

        var description = $"*The {formName} falls away, and {character.Name} stands in their own shape.*";
        if (shed)
            description += $"\n\n**{ShapeshiftRules.Shed}:** the damage is negated.";
        if (lost > 0)
            description += $"\n\n{lost} {ShapeshiftRules.HoldName} lost.";

        return await ReplyAsync($"🌿 {character.Name} returns to their natural form", description);
    }

    /// <summary>
    /// Rolls 2d6 + a stat, with its debility and any subclass bonuses.
    /// </summary>
    /// <returns>The roll, and what went into it for the "Rolled with" field.</returns>
    private async Task<(MoveRoll Roll, string RolledWith)> RollStatAsync(Character character, StatType stat, int bonus)
    {
        var modifier = character.Stats.FirstOrDefault(s => s.StatType == stat)?.Modifier ?? 0;
        var debility = DebilityRules.RollPenalty(character, stat);
        var bonuses = await damageService.BonusesAsync(character, RollKind.Stat, stat);

        var parts = new List<string> { $"{stat.ToString().ToUpper()} {FormatSigned(modifier)}" };
        if (debility is { } penalty)
            parts.Add($"{penalty.Debility} {FormatSigned(penalty.Penalty)}");
        parts.AddRange(bonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}"));
        if (bonus != 0)
            parts.Add($"Bonus {FormatSigned(bonus)}");

        var roll = MoveRoller.Roll(diceRoller, modifier + (debility?.Penalty ?? 0) + bonuses.Sum(b => b.Amount) + bonus);
        return (roll, string.Join(", ", parts));
    }

    private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private static string Article(string name) => "aeiou".Contains(char.ToLowerInvariant(name[0])) ? "an" : "a";

    // Embed field values are capped at 1024 characters
    private static string Truncate(string value) => value.Length <= 1024 ? value : value[..1021] + "...";

    private static string UnknownForm(Character character, string name) =>
        $"{character.Name} doesn't know a form called {name.Trim()}. Add it with /shapeshift learn";

    /// <returns>The user's character if they can shapeshift, or why not.</returns>
    private async Task<(Character? Character, string? Error)> GetShapeshifterAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return (null, "No character exists. Try making one with /character create");

        if (!ShapeshiftRules.CanShapeshift(character))
            return (null, $"{character.Name} doesn't know {ShapeshiftRules.Shapeshifter}.");

        return (character, null);
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());

    private async Task<IResult> ReplyWithRollAsync(Character character, string title, string flavor, MoveRoll roll, IReadOnlyList<IEmbedField> fields)
    {
        var embed = new Embed(
            Title: title,
            Description: $"{flavor}\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
            Fields: fields.ToList(),
            Colour: MoveRoller.ColorFor(roll.Outcome)
        );

        var reply = await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);

        if (reply.IsSuccess && _context.TryGetChannelID(out var channelID))
            await rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, roll.Dice, RollLabel.Of(embed) with { Move = ShapeshiftRules.Shapeshifter });

        return reply;
    }
}
