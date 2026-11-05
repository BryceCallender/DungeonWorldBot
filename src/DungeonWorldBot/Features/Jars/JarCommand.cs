using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
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
using Remora.Results;

namespace DungeonWorldBot.Features.Jars;

/// <summary>
/// The Necromancer's Hexed Canopic Jars: storing corpses, dropping them out, and raising them.
/// </summary>
[Group("jar")]
public class JarCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    CharacterMoves characterMoves,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService,
    RollReveal rollReveal,
    IJarService jarService)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService, rollReveal)
{
    private const string CorpseDescription = "A corpse in one of your jars";

    [Command("store")]
    [Description("Spend a few minutes storing a corpse of any size in an empty jar")]
    public async Task<IResult> StoreAsync(
        [Description("Who or what it was, e.g. A Skinless Hound")]
        string corpse)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotCollect(character) is { } notCollector)
            return await ReplyWithErrorAsync(notCollector);

        if (JarRules.WhyCannotStore(character!, corpse) is { } cannotStore)
            return await ReplyAsync("Jars Full", cannotStore, [JarsField(character!)]);

        await jarService.StoreAsync(character!, corpse);

        return await ReplyAsync($"🏺 {character!.Name} stores {corpse.Trim()}", "*The jar's lid seals with a wet hiss...*", [JarsField(character)]);
    }

    [Command("open")]
    [Description("Open a jar, dropping its corpse in front of you")]
    public async Task<IResult> OpenAsync(
        [Description(CorpseDescription)] [AutocompleteProvider(JarAutocomplete.ID)]
        string corpse)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotCollect(character) is { } notCollector)
            return await ReplyWithErrorAsync(notCollector);

        if (JarRules.Find(character!, corpse) is not { } stored)
            return await ReplyWithErrorAsync(NotStored(character!, corpse));

        await jarService.EmptyAsync(character!, stored);

        return await ReplyAsync($"⚱️ {character!.Name} opens a jar", $"*{stored} spills out onto the ground.*", [JarsField(character)]);
    }

    [Command("list")]
    [Description("See what's in a Necromancer's jars")]
    public async Task<IResult> ListAsync(
        [Description("The user to see the jars of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = await _characterService.GetCharacterToShowAsync(user?.ID ?? GetUserId());

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? NoCharacterMessage
                : $"{user.Username} does not have a character.");
        }

        if (!JarRules.CanCollect(character))
            return await ReplyWithErrorAsync($"{character.Name} doesn't keep corpses in jars.");

        return await ReplyAsync($"{character.Name}'s {JarRules.JarsName}", JarRules.Describe(character));
    }

    [Command("raise")]
    [Description("Raise the Dead (+INT) with a corpse from one of your jars")]
    public async Task<IResult> RaiseAsync(
        [Description(CorpseDescription)] [AutocompleteProvider(JarAutocomplete.ID)]
        string corpse,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotCollect(character) is { } notCollector)
            return await ReplyWithErrorAsync(notCollector);

        if (JarRules.Find(character!, corpse) is not { } stored)
            return await ReplyWithErrorAsync(NotStored(character!, corpse));

        var raise = characterMoves.PrepareClassMove(character!, JarRules.RaiseTheDead);
        if (raise.Error is { } error)
            return await ReplyWithErrorAsync(error);

        var input = raise.Input!;
        return await ReplyWithMoveAsync(
            character!,
            raise.Move!,
            input.ModifierLabel,
            input.Modifier,
            bonus,
            input.Stat,
            prepared: prepared,
            aftermath: resolution => RisesAsync(resolution, stored));
    }

    /// <summary>
    /// On a hit the corpse leaves its jar and rises with its Power. On a miss it stays put.
    /// </summary>
    private async Task<IReadOnlyList<IEmbedField>> RisesAsync(MoveResolution resolution, string corpse)
    {
        var character = resolution.Character;
        if (resolution.Outcome == MoveOutcome.Miss)
            return [new EmbedField("🏺 Still sealed", $"{corpse} stays in its jar.")];

        await jarService.EmptyAsync(character, corpse);
        await _resourceService.SetValueAsync(character, JarRules.Power, JarRules.RaisedPower);

        var power = $"{JarRules.Power.Name}: {_resourceService.Format(character, JarRules.Power)}";
        if (resolution.Outcome == MoveOutcome.WeakHit)
            power += $"\nIf you chose *the flesh resists you*, it starts with 1 less: /resource spend {JarRules.Power.Name}";

        return
        [
            new EmbedField($"💀 {corpse} rises", power),
            JarsField(character),
        ];
    }

    private static IEmbedField JarsField(Character character) =>
        new EmbedField(JarRules.JarsName, JarRules.Describe(character));

    private static string NotStored(Character character, string corpse) =>
        $"None of {character.Name}'s jars hold {corpse.Trim()}. See them with /jar list";

    private static string? WhyCannotCollect(Character? character)
    {
        if (character is null)
            return NoCharacterMessage;
        return JarRules.CanCollect(character) ? null : $"{character.Name} doesn't know {JarRules.CorpseCollector}.";
    }
}
