using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Spirits;

/// <summary>
/// The Shaman's Spirit Catcher: binding a dying creature's spirit, recording what it does, and releasing it.
/// </summary>
[Group("spirit")]
public class SpiritCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    CharacterMoves characterMoves,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService,
    RollReveal rollReveal,
    IAbilityService abilityService)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService, rollReveal)
{
    [Command("catch")]
    [Description("Bind the spirit of a creature you saw die to your Spirit Catcher totem (+WIS)")]
    public async Task<IResult> CatchAsync(
        [Description("The creature whose spirit you're catching, e.g. a dire wolf")]
        string spirit,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotCatch(character) is { } cannotCatch)
            return await ReplyWithErrorAsync(cannotCatch);

        if (SpiritCatcherRules.Spirit(character!) is { } held)
            return await ReplyWithErrorAsync($"{character!.Name}'s Spirit Catcher already holds {held}. Release it with /spirit release first.");

        var move = characterMoves.PrepareClassMove(character!, SpiritCatcherRules.SpiritCatcher);
        if (move.Error is { } error)
            return await ReplyWithErrorAsync(error);

        var input = move.Input!;
        return await ReplyWithMoveAsync(
            character!,
            move.Move!,
            input.ModifierLabel,
            input.Modifier,
            bonus,
            input.Stat,
            prepared: prepared,
            aftermath: resolution => BindAsync(resolution, spirit.Trim()));
    }

    [Command("effect")]
    [Description("Record what your caught spirit does when released, as the GM tells you")]
    public async Task<IResult> EffectAsync(
        [Description("Its supernatural effect when released")]
        string effect)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotCatch(character) is { } cannotCatch)
            return await ReplyWithErrorAsync(cannotCatch);

        if (SpiritCatcherRules.Spirit(character!) is not { } spirit)
            return await ReplyWithErrorAsync($"{character!.Name}'s Spirit Catcher is empty.");

        await abilityService.SetDescriptionAsync(SpiritCatcherRules.Catcher(character!)!, SpiritCatcherRules.Describe(spirit, effect));

        return await ReplyAsync($"🪶 {character!.Name}'s {SpiritCatcherRules.SpiritCatcher}", SpiritCatcherRules.Status(character));
    }

    [Command("release")]
    [Description("Release your caught spirit for its effect. It moves on afterwards")]
    public async Task<IResult> ReleaseAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotCatch(character) is { } cannotCatch)
            return await ReplyWithErrorAsync(cannotCatch);

        if (SpiritCatcherRules.Spirit(character!) is not { } spirit)
            return await ReplyWithErrorAsync($"{character!.Name}'s Spirit Catcher is empty.");

        var effect = SpiritCatcherRules.Effect(character!);
        await abilityService.SetDescriptionAsync(SpiritCatcherRules.Catcher(character!)!, SpiritCatcherRules.Move.Summary);

        var description = $"*The spirit of {spirit} bursts from the totem...*";
        if (effect is not null)
            description += $"\n\n{effect}";
        description += "\n\nIts work done, it moves on.";
        return await ReplyAsync($"🪶 {character!.Name} releases {spirit}", description);
    }

    // On a hit the spirit is bound. A 7-9 also brings the GM's complication.
    private async Task<IReadOnlyList<IEmbedField>> BindAsync(MoveResolution resolution, string spirit)
    {
        if (resolution.Outcome == MoveOutcome.Miss)
            return [new EmbedField("🪶 Spirit Catcher", $"The spirit of {spirit} slips away.")];

        var character = resolution.Character;
        await abilityService.SetDescriptionAsync(SpiritCatcherRules.Catcher(character)!, SpiritCatcherRules.Describe(spirit, null));

        var description = SpiritCatcherRules.Status(character);
        if (resolution.Outcome == MoveOutcome.WeakHit)
            description += "\nThe GM adds a complication: it's unpredictable, has an unwanted side effect, or must be used soon.";
        return [new EmbedField("🪶 Spirit Catcher", description)];
    }

    private static string? WhyCannotCatch(Character? character)
    {
        if (character is null)
            return NoMoverMessage;
        return SpiritCatcherRules.Catcher(character) is null ? $"{character.Name} doesn't know {SpiritCatcherRules.SpiritCatcher}." : null;
    }
}
