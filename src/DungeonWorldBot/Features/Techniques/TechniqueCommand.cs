using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Techniques;

/// <summary>
/// Tags typed by the player, combined into one value, or why they can't be.
/// </summary>
public record TagChoice(TechniqueTag Tags, string? Error = null);

/// <summary>
/// The Channeler's Techniques: developing them, and channeling one with its tags doing their work.
/// </summary>
[Group("technique")]
public class TechniqueCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    CharacterMoves characterMoves,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService,
    RollReveal rollReveal,
    ITechniqueService techniqueService,
    IDamageService damageService,
    IDiceRoller diceRoller)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService, rollReveal)
{
    private const string TechniqueDescription = "One of the Techniques you know";
    private const string TagDescription = "A tag, like Projectile or Barrier";
    private const string DevastationDie = "1d4";

    [Command("develop")]
    [Description("Develop a new Technique: a name and its tags")]
    public async Task<IResult> DevelopAsync(
        [Description("What you call it, e.g. Ember Lance")]
        string name,
        [Description(TagDescription)] [AutocompleteProvider(TechniqueTagAutocomplete.ID)]
        string tag,
        [Description(TagDescription)] [AutocompleteProvider(TechniqueTagAutocomplete.ID)] [Option("second-tag")]
        string secondTag,
        [Description("A third tag, for your Signature Technique")] [AutocompleteProvider(TechniqueTagAutocomplete.ID)] [Option("third-tag")]
        string? thirdTag = null,
        [Description("Make it your Signature Technique, which has 3 tags (needs the Signature Technique move)")]
        bool signature = false)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotChannel(character) is { } notChanneler)
            return await ReplyWithErrorAsync(notChanneler);

        if (TechniqueRules.WhyCannotDevelop(character!, name, signature) is { } cannotDevelop)
            return await ReplyWithErrorAsync(cannotDevelop);

        var choice = ParseTags(tag, secondTag, thirdTag);
        if (choice.Error is not null)
            return await ReplyWithErrorAsync(choice.Error);

        if (TechniqueRules.WhyCannotHave(character!, new Technique { IsSignature = signature }, choice.Tags) is { } badTags)
            return await ReplyWithErrorAsync(badTags);

        var technique = await techniqueService.DevelopAsync(character!, name, choice.Tags, signature);

        return await ReplyAsync($"🌀 {character!.Name} develops {technique.Name}", Summary(character, technique));
    }

    [Command("edit")]
    [Description("Rename a Technique or replace its tags")]
    public async Task<IResult> EditAsync(
        [Description(TechniqueDescription)] [AutocompleteProvider(TechniqueAutocomplete.ID)]
        string technique,
        [Description("A new name. Leave blank to keep it")] [Option("new-name")]
        string? newName = null,
        [Description("A new tag. Give them all to replace the tags, or none to keep them")] [AutocompleteProvider(TechniqueTagAutocomplete.ID)]
        string? tag = null,
        [Description(TagDescription)] [AutocompleteProvider(TechniqueTagAutocomplete.ID)] [Option("second-tag")]
        string? secondTag = null,
        [Description("A third tag, for your Signature Technique")] [AutocompleteProvider(TechniqueTagAutocomplete.ID)] [Option("third-tag")]
        string? thirdTag = null)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotChannel(character) is { } notChanneler)
            return await ReplyWithErrorAsync(notChanneler);

        if (TechniqueRules.Find(character!, technique) is not { } known)
            return await ReplyWithErrorAsync(UnknownTechnique(character!, technique));

        if (string.IsNullOrWhiteSpace(newName) && tag is null && secondTag is null && thirdTag is null)
            return await ReplyWithErrorAsync("Give a new name or new tags for the Technique.");

        if (!string.IsNullOrWhiteSpace(newName) && TechniqueRules.Find(character!, newName) is { } taken && taken != known)
            return await ReplyWithErrorAsync($"{character!.Name} already has a Technique called {taken.Name}.");

        TechniqueTag? tags = null;
        if (tag is not null || secondTag is not null || thirdTag is not null)
        {
            var choice = ParseTags(tag, secondTag, thirdTag);
            if (choice.Error is not null)
                return await ReplyWithErrorAsync(choice.Error);
            if (TechniqueRules.WhyCannotHave(character!, known, choice.Tags) is { } badTags)
                return await ReplyWithErrorAsync(badTags);
            tags = choice.Tags;
        }

        await techniqueService.UpdateAsync(known, newName, tags);

        return await ReplyAsync($"🌀 {character!.Name}'s {known.Name}", Summary(character, known));
    }

    [Command("forget")]
    [Description("Let go of a Technique to make room for a new one")]
    public async Task<IResult> ForgetAsync(
        [Description(TechniqueDescription)] [AutocompleteProvider(TechniqueAutocomplete.ID)]
        string technique)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotChannel(character) is { } notChanneler)
            return await ReplyWithErrorAsync(notChanneler);

        if (TechniqueRules.Find(character!, technique) is not { } known)
            return await ReplyWithErrorAsync(UnknownTechnique(character!, technique));

        await techniqueService.ForgetAsync(character!, known);

        return await ReplyAsync("Technique Forgotten", $"{character!.Name} lets {known.Name} go.");
    }

    [Command("list")]
    [Description("See the Techniques a character knows")]
    public async Task<IResult> ListAsync(
        [Description("The user to see the Techniques of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = await _characterService.GetCharacterToShowAsync(TargetOf(user));

        if (character is null)
            return await ReplyNoCharacterAsync(user);

        if (!TechniqueRules.CanChannel(character))
            return await ReplyWithErrorAsync($"{character.Name} can't Channel.");

        var techniques = character.Techniques.OrderByDescending(t => t.IsSignature).ThenBy(t => t.Name).ToList();
        var fields = techniques
            .Select(t => (IEmbedField)new EmbedField(Heading(t), TechniqueRules.Describe(t, character)))
            .ToList();

        var description = $"{techniques.Count} of {TechniqueRules.MaxTechniques(character)} Techniques";
        if (techniques.Count == 0)
            description += "\n\nNo Techniques yet. Develop one with /technique develop";

        return await ReplyAsync($"{character.Name}'s Techniques", description, fields);
    }

    [Command("channel")]
    [Description("Channel (+CON) using one of your Techniques, rolling its damage on a hit")]
    public async Task<IResult> ChannelAsync(
        [Description(TechniqueDescription)] [AutocompleteProvider(TechniqueAutocomplete.ID)]
        string technique,
        [Description("How far the target is, when the Technique has both Blast (close) and Projectile (near)")]
        TechniqueRange? range = null,
        [Description("The enemy you're aiming at, to take the damage off it on a hit")] [AutocompleteProvider(TargetAutocomplete.ID)]
        string? target = null,
        [Description("Thread the Needle: d4s of bonus damage. Each showing 1 or 2 hurts you too")] [MinValue(0)] [Option("thread")]
        int threadDice = 0,
        [Description("Devastation Wave: a fourth tag for your Signature Technique, at the cost of 1d4 damage")]
        [AutocompleteProvider(TechniqueTagAutocomplete.ID)]
        string? devastate = null,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription + ", if it deals damage")]
        int extraDamage = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotChannel(character) is { } notChanneler)
            return await ReplyWithErrorAsync(notChanneler);

        if (TechniqueRules.Find(character!, technique) is not { } known)
            return await ReplyWithErrorAsync(UnknownTechnique(character!, technique));

        var tags = known.Tags;
        TechniqueTag? wave = null;
        if (devastate is not null)
        {
            if (TechniqueRules.ParseTag(devastate) is not { } extraTag)
                return await ReplyWithErrorAsync(UnknownTag(devastate));
            if (TechniqueRules.WhyCannotDevastate(character!, known, extraTag) is { } cannotDevastate)
                return await ReplyWithErrorAsync(cannotDevastate);
            wave = extraTag;
            tags |= extraTag;
        }

        if (TechniqueRules.NeedsRange(tags, range))
            return await ReplyWithErrorAsync($"{known.Name} has both Blast and Projectile. Pick a range: close for Blast, near for Projectile.");

        var damage = TechniqueRules.DamageOf(tags, range);
        if (WhyCannotThread(character!, damage, threadDice) is { } cannotThread)
            return await ReplyWithErrorAsync(cannotThread);

        var channel = characterMoves.PrepareClassMove(character!, TechniqueRules.Channel);
        if (channel.Error is { } error)
            return await ReplyWithErrorAsync(error);

        var input = channel.Input!;
        var moveDamage = damage is null ? null : new MoveDamage(damage.Dice, damage.Tag.ToString(), damage.Piercing, threadDice);
        return await ReplyWithMoveAsync(
            character!,
            channel.Move! with { RollsDamage = damage is not null },
            input.ModifierLabel,
            input.Modifier,
            bonus,
            input.Stat,
            target: target,
            extraDamage: extraDamage,
            prepared: prepared,
            damage: moveDamage,
            aftermath: resolution => AftermathAsync(resolution, known, tags, wave));
    }

    /// <summary>
    /// The Technique's tags, then what they did: Barrier armor on a hit, and Devastation Wave's cost.
    /// </summary>
    /// <remarks>
    /// Runs only once the Channel is actually rolled, so a Channel that's refused (say, for lack of Preparation) leaves
    /// the last Barrier standing.
    /// </remarks>
    private async Task<IReadOnlyList<IEmbedField>> AftermathAsync(MoveResolution resolution, Technique technique, TechniqueTag tags, TechniqueTag? wave)
    {
        var character = resolution.Character;

        // Barrier only lasts until the next Channel, which is this one
        var barrierFaded = (await techniqueService.DropBarrierAsync(character)).Count > 0;

        var tagLines = TechniqueRules.TagsOf(tags).Select(t => $"• **{t}{(t == wave ? $" ({TechniqueRules.DevastationWave})" : string.Empty)}:** {TechniqueRules.Describe(t, character)}");
        List<IEmbedField> fields = [new EmbedField($"🌀 {technique.Name}", string.Join('\n', tagLines))];

        var isHit = resolution.Outcome != MoveOutcome.Miss;
        if (isHit && tags.HasFlag(TechniqueTag.Barrier))
        {
            var armor = await techniqueService.RaiseBarrierAsync(character);
            fields.Add(new EmbedField("🛡️ Barrier", $"+{armor} armor until your next Channel."));
        }
        else if (barrierFaded)
        {
            fields.Add(new EmbedField("🛡️ Barrier", "Your last Barrier fades."));
        }

        if (wave is not null)
        {
            var cost = diceRoller.Roll(DevastationDie).Value;
            var hurt = await damageService.TakeAsync(character, cost, DamageType.True);
            fields.Add(new EmbedField(TechniqueRules.DevastationWave, $"{DevastationDie} → {cost}\n{hurt.Describe(character)}"));
        }

        return fields;
    }

    private static string? WhyCannotThread(Character character, TechniqueDamage? damage, int threadDice)
    {
        if (threadDice == 0)
            return null;
        if (damage is null)
            return $"{TechniqueRules.ThreadTheNeedle} only adds to a Technique that deals damage.";

        var most = TechniqueRules.MaxThreadDice(character);
        if (most == 0)
            return $"{character.Name} needs the {TechniqueRules.ThreadTheNeedle} move to add bonus damage.";
        if (threadDice <= most)
            return null;

        return most == 1
            ? $"{character.Name} can add 1 {TechniqueRules.ThreadTheNeedle} die. {TechniqueRules.Fissure} allows {TechniqueRules.FissureDice}."
            : $"{character.Name} can add at most {most} {TechniqueRules.ThreadTheNeedle} dice.";
    }

    private static TagChoice ParseTags(params string?[] values)
    {
        var tags = TechniqueTag.None;
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            if (TechniqueRules.ParseTag(value) is not { } tag)
                return new TagChoice(tags, UnknownTag(value!));
            if (tags.HasFlag(tag))
                return new TagChoice(tags, $"{tag} is in there twice. Each tag goes on a Technique once.");
            tags |= tag;
        }

        return new TagChoice(tags);
    }

    private static string Summary(Character character, Technique technique) =>
        $"*{Heading(technique)}*\n\n{TechniqueRules.Describe(technique, character)}";

    private static string Heading(Technique technique) =>
        technique.IsSignature ? $"⭐ {technique.Name} (Signature)" : technique.Name;

    private static string UnknownTag(string value) =>
        $"{value.Trim()} isn't a Technique tag. Try one of: {string.Join(", ", TechniqueRules.AllTags)}";

    private static string UnknownTechnique(Character character, string name) =>
        $"{character.Name} doesn't know a Technique called {name.Trim()}. Develop one with /technique develop";

    private static string? WhyCannotChannel(Character? character)
    {
        if (character is null)
            return NoCharacterMessage;
        return TechniqueRules.CanChannel(character) ? null : $"{character.Name} doesn't know {TechniqueRules.Channel}.";
    }
}
