using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Brands;

/// <summary>
/// The Immolator's Burning Brand: conjuring it with its tags, and attacking with it until its uses burn out.
/// </summary>
[Group("brand")]
public class BrandCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    CharacterMoves characterMoves,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService,
    RollReveal rollReveal,
    IBrandService brandService)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService, rollReveal)
{
    private const string TagDescription = "A tag for the brand: two on a 10+, one on a 7-9";
    private const string TargetDescription = "The enemy you're attacking, to take your damage off it on a hit";
    private const string StatDescription = "Attack with INT (default), or the usual stat for the move";

    [Command("conjure")]
    [Description("Conjure a weapon of pure flame (+CON), choosing its tags")]
    public async Task<IResult> ConjureAsync(
        [Description(TagDescription)] [AutocompleteProvider(BrandTagAutocomplete.ID)]
        string tag,
        [Description(TagDescription)] [AutocompleteProvider(BrandTagAutocomplete.ID)] [Option("second-tag")]
        string? secondTag = null,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotConjure(character) is { } cannotConjure)
            return await ReplyWithErrorAsync(cannotConjure);

        List<string> tags = [];
        foreach (var typed in new[] { tag, secondTag }.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            if (BrandRules.FindOption(typed!) is not { } option)
                return await ReplyWithErrorAsync($"{typed!.Trim()} isn't a Burning Brand tag. Choose from: {string.Join("; ", BrandRules.Options)}");
            if (tags.Contains(option))
                return await ReplyWithErrorAsync($"{option} is in there twice.");
            tags.Add(option);
        }

        var brand = characterMoves.PrepareClassMove(character!, BrandRules.BurningBrand);
        if (brand.Error is { } error)
            return await ReplyWithErrorAsync(error);

        var input = brand.Input!;
        return await ReplyWithMoveAsync(
            character!,
            brand.Move!,
            input.ModifierLabel,
            input.Modifier,
            bonus,
            input.Stat,
            prepared: prepared,
            aftermath: resolution => ForgeAsync(resolution, tags));
    }

    [Command("strike")]
    [Description("Hack and Slash with your Burning Brand (+INT), using one of its uses")]
    public Task<IResult> StrikeAsync(
        [Description(TargetDescription)] [AutocompleteProvider(TargetAutocomplete.ID)] string? target = null,
        [Description(StatDescription)] StatType stat = StatType.Int,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        => AttackAsync(BasicMoves.HackAndSlash, stat, target, bonus, prepared, extraDamage);

    [Command("throw")]
    [Description("Volley your Burning Brand (+INT) if it's thrown, using one of its uses")]
    public Task<IResult> ThrowAsync(
        [Description(TargetDescription)] [AutocompleteProvider(TargetAutocomplete.ID)] string? target = null,
        [Description(StatDescription)] StatType stat = StatType.Int,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        => AttackAsync(BasicMoves.Volley, stat, target, bonus, prepared, extraDamage);

    private async Task<IResult> AttackAsync(MoveDefinition move, StatType stat, string? target, int bonus, bool prepared, int extraDamage)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotConjure(character) is { } cannotConjure)
            return await ReplyWithErrorAsync(cannotConjure);

        if (BrandRules.Brand(character!) is not { } brand)
            return await ReplyWithErrorAsync($"{character!.Name} has no Burning Brand lit. Conjure one with /brand conjure");

        if (character!.Stats.FirstOrDefault(s => s.StatType == stat) is not { } rolled)
            return await ReplyWithErrorAsync($"Your character has no {stat} stat.");

        return await ReplyWithMoveAsync(
            character,
            move,
            stat.ToString().ToUpper(),
            rolled.Modifier,
            bonus,
            stat,
            target: target,
            extraDamage: extraDamage + brand.Damage,
            prepared: prepared,
            aftermath: _ => BurnAsync(character, brand));
    }

    /// <summary>
    /// On a hit the brand flares into being with the tags the roll allows. On a miss, nothing catches.
    /// </summary>
    private async Task<IReadOnlyList<IEmbedField>> ForgeAsync(MoveResolution resolution, IReadOnlyList<string> wanted)
    {
        var allowed = BrandRules.TagsFor(resolution.Outcome);
        if (allowed == 0)
            return [new EmbedField("🔥 Burning Brand", "The flame won't take shape.")];

        var tags = wanted.Take(allowed).ToList();
        var brand = await brandService.ConjureAsync(resolution.Character, tags);

        var description = $"**{brand.Name}**\n{_resourceService.Format(resolution.Character, BrandRules.UsesResource)} uses. Attack with /brand strike or /brand throw";
        if (wanted.Count > tags.Count)
            description += $"\n*Only {allowed} tag on a 7-9, so {string.Join(", ", wanted.Skip(allowed))} didn't take.*";
        return [new EmbedField("🔥 Burning Brand", description)];
    }

    // Each attack uses one, hit or miss
    private async Task<IReadOnlyList<IEmbedField>> BurnAsync(Character character, Weapon brand)
    {
        var left = await brandService.SpendAsync(character);
        var description = left > 0
            ? $"{_resourceService.Format(character, BrandRules.UsesResource)} uses left."
            : "That was its last use: the brand gutters out.";
        if (brand.Damage > 0)
            description += $"\nIncludes the brand's +{brand.Damage} damage on a hit.";
        return [new EmbedField($"🔥 {brand.Name}", description)];
    }

    private static string? WhyCannotConjure(Character? character)
    {
        if (character is null)
            return NoMoverMessage;
        return BrandRules.CanConjure(character) ? null : $"{character.Name} doesn't know {BrandRules.BurningBrand}.";
    }
}

/// <summary>
/// The tags a Burning Brand can be conjured with.
/// </summary>
public class BrandTagAutocomplete : SuggestionAutocomplete
{
    public const string ID = "brand-tag";

    public override string Identity => ID;

    protected override IEnumerable<Suggestion> AllCandidates() => BrandRules.Options.Select(o => new Suggestion(o, o));
}
