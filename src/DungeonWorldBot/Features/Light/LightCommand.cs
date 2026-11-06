using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.InverseWorld;
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
using Remora.Results;

namespace DungeonWorldBot.Features.Light;

/// <summary>
/// The Lantern's little light as a weapon: loosing Arrows of Light, and forming or lending the Shield of Light.
/// </summary>
[Group("light")]
public class LightCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService,
    RollReveal rollReveal,
    ILightService lightService)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService, rollReveal)
{
    private static ClassResource ArrowPiercing => Lantern.Sheet.FindResource(Lantern.ArrowPiercing)!;

    [Command("arrows")]
    [Description("Volley with Arrows of Light (+INT), piercing armor")]
    public async Task<IResult> ArrowsAsync(
        [Description("The enemy you're shooting, to take your damage off it on a hit")] [AutocompleteProvider(TargetAutocomplete.ID)]
        string? target = null,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotWield(character) is { } cannotWield)
            return await ReplyWithErrorAsync(cannotWield);

        if (character!.Stats.FirstOrDefault(s => s.StatType == StatType.Int) is not { } intelligence)
            return await ReplyWithErrorAsync("Your character has no INT stat.");

        var piercing = _resourceService.GetValue(character, ArrowPiercing);
        return await ReplyWithMoveAsync(
            character,
            BasicMoves.Volley,
            "INT (mystical)",
            intelligence.Modifier,
            bonus,
            StatType.Int,
            target: target,
            extraDamage: extraDamage,
            prepared: prepared,
            piercing: piercing,
            aftermath: resolution => ArrowNotesAsync(resolution, piercing));
    }

    [Command("shield")]
    [Description("Form the Shield of Light (+1 armor) around you, or lend it to an ally within reach")]
    public async Task<IResult> ShieldAsync(
        [Description("The ally to lend it to. Leave blank to keep it yourself")]
        IUser? ally = null)
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotWield(character) is { } cannotWield)
            return await ReplyWithErrorAsync(cannotWield);

        var holder = character!;
        if (ally is not null && ally.ID != character!.ID)
        {
            if (await _characterService.GetCharacterFromUserAsync(ally.ID) is not { } allyCharacter)
                return await ReplyNoCharacterAsync(ally);
            holder = allyCharacter;
        }

        var shielded = await lightService.RaiseShieldAsync(character!, holder);

        var description = holder.ID == character!.ID
            ? $"*{character!.Name}'s little light spreads into a shimmering shield.*"
            : $"*{character!.Name}'s little light drifts to {holder.Name} and spreads into a shimmering shield.*";
        var fields = shielded
            .Select(c => (IEmbedField)new EmbedField($"🛡️ {c.Name}", $"+{LightService.ShieldArmor} armor until the shield fades. Armor is now {DamageRules.Armor(c, ArmorType.Physical)}.", IsInline: true))
            .ToList();

        return await ReplyAsync($"✨ {Lantern.ShieldOfLight}", description, fields);
    }

    [Command("dim")]
    [Description("Your little light lets its Shield of Light fade and returns to its harmless form")]
    public async Task<IResult> DimAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (WhyCannotWield(character) is { } cannotWield)
            return await ReplyWithErrorAsync(cannotWield);

        var lowered = await lightService.LowerShieldAsync(character!);

        return await ReplyAsync(
            $"🕯️ {character!.Name}'s little light dims",
            lowered ? "The Shield of Light fades, and the light floats back to its lantern." : "The light floats back to its lantern.");
    }

    // Runs only once the Volley is actually rolled, so a refused one leaves the shield where it was
    private async Task<IReadOnlyList<IEmbedField>> ArrowNotesAsync(MoveResolution resolution, int piercing)
    {
        var character = resolution.Character;

        // Without Illuminated Warrior the light holds one form, so taking up arrows lets the shield go
        var shieldFaded = !ClassSheetLibrary.Knows(character, Lantern.IlluminatedWarrior) && await lightService.LowerShieldAsync(character);

        List<IEmbedField> fields = [];
        if (shieldFaded)
            fields.Add(new EmbedField("🛡️ Shield of Light", "The light leaves your shield to become arrows."));

        var note = resolution.Outcome switch
        {
            MoveOutcome.WeakHit => $"Piercing {piercing}. If you choose to spend ammo, lose 1 piercing instead with /resource spend {Lantern.ArrowPiercing}.",
            MoveOutcome.Miss => "Your little light reverts to its harmless form and sulks for a while.",
            _ => $"Piercing {piercing}.",
        };
        fields.Add(new EmbedField($"✨ Arrows of Light ({_resourceService.Format(character, ArrowPiercing)} piercing)", note));
        return fields;
    }

    private static string? WhyCannotWield(Character? character)
    {
        if (character is null)
            return NoMoverMessage;
        return ClassSheetLibrary.Knows(character, Lantern.LightWeaponry) ? null : $"{character.Name} doesn't know {Lantern.LightWeaponry}.";
    }
}
