using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Npcs;

/// <summary>
/// Looking after the NPCs travelling with the party. The DM adds and changes them with /dm npc.
/// </summary>
[Group("npc")]
public class NpcCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    INpcService npcService,
    IDiceRoller diceRoller,
    IEncounterService encounterService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string NameDescription = "The NPC";

    [Command("list")]
    [Description("List the NPCs travelling with the party")]
    public async Task<IResult> ListAsync()
    {
        var npcs = await npcService.GetAllAsync();
        if (npcs.Count == 0)
            return await ReplyWithErrorAsync("No NPCs are with the party. The DM can add one with /dm npc add.");

        // Discord allows 25 fields per embed
        var fields = npcs.Take(25)
            .Select(n => (IEmbedField)new EmbedField(NpcSheet.Title(n), NpcSheet.Summary(n)))
            .ToList();

        return await ReplyAsync("Party NPCs", npcs.Count > 25 ? $"Showing 25 of {npcs.Count}." : string.Empty, fields);
    }

    [Command("show")]
    [Description("Show an NPC's sheet")]
    public async Task<IResult> ShowAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(NpcAutocomplete.ID)]
        string name)
    {
        var npc = await npcService.FindAsync(name);
        if (npc is null)
            return await ReplyWithErrorAsync(NotFound(name));

        return await ReplyAsync(NpcSheet.Title(npc), npc.CurrentHP == 0 ? "**Down at 0 HP**" : string.Empty, NpcSheet.Fields(npc));
    }

    [Command("damage")]
    [Description("Damage an NPC, reduced by their armor if it's physical")]
    public async Task<IResult> DamageAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(NpcAutocomplete.ID)]
        string name,
        [Description("Amount to damage, before armor. Can be a dice roll or just flat damage")]
        string damage,
        [Description("Physical damage is stopped by their armor; anything else goes straight through")]
        DamageType type = DamageType.Physical,
        [Description("How much of their armor the hit ignores")]
        [MinValue(0)]
        int piercing = 0)
    {
        var npc = await npcService.FindAsync(name);
        if (npc is null)
            return await ReplyWithErrorAsync(NotFound(name));

        if (!diceRoller.TryRoll(damage, out var roll))
            return await ReplyWithErrorAsync($"`{damage}` is not a valid amount or dice roll.");

        var hit = await npcService.DamageAsync(npc, roll.Value, type, piercing);

        return await ReplyAsync("Damaged!", NpcSheet.DescribeHit(npc, hit));
    }

    [Command("heal")]
    [Description("Heal an NPC")]
    public async Task<IResult> HealAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(NpcAutocomplete.ID)]
        string name,
        [Description("Amount to heal. Can be a dice roll or left blank to full heal")]
        string? heal = null)
    {
        var npc = await npcService.FindAsync(name);
        if (npc is null)
            return await ReplyWithErrorAsync(NotFound(name));

        string description;
        if (string.IsNullOrWhiteSpace(heal))
        {
            await npcService.HealAsync(npc);
            description = $"{npc.Name} was fully healed";
        }
        else
        {
            if (!diceRoller.TryRoll(heal, out var roll))
                return await ReplyWithErrorAsync($"`{heal}` is not a valid amount or dice roll.");

            var amount = Math.Max(0, roll.Value);
            await npcService.HealAsync(npc, amount);
            description = $"{npc.Name} was healed for {amount} HP";
        }

        return await ReplyAsync("Healed!", $"{description} ({npc.CurrentHP}/{npc.MaxHP} HP)");
    }

    [Command("roll")]
    [Description("Roll for an NPC: 2d6 plus a stat, their loyalty or a skill, or their damage dice")]
    public async Task<IResult> RollAsync(
        [Description(NameDescription)]
        [AutocompleteProvider(NpcAutocomplete.ID)]
        string name,
        [Description("A stat (STR, DEX...), Loyalty, one of their skills, or Damage")]
        [AutocompleteProvider(NpcRollAutocomplete.ID)]
        string with,
        [Description("Anything else added to the roll, like +1 from aid or -1 forward")]
        int bonus = 0,
        [Description("The enemy they're attacking. A damage roll is taken off it")]
        [AutocompleteProvider(TargetAutocomplete.ID)]
        string? target = null)
    {
        var npc = await npcService.FindAsync(name);
        if (npc is null)
            return await ReplyWithErrorAsync(NotFound(name));

        var option = NpcRolls.Find(npc, with);
        if (option is null)
        {
            var choices = string.Join(", ", NpcRolls.Options(npc).Select(o => o.Name));
            return await ReplyWithErrorAsync($"{npc.Name} can't roll with {with}. Try one of: {choices}.");
        }

        var dice = DiceHelper.AddModifier(option.Dice, bonus);
        if (!diceRoller.TryRoll(dice, out var roll))
            return await ReplyWithErrorAsync($"{npc.Name}'s {option.Name} dice (`{option.Dice}`) can't be rolled. The DM can fix them with /dm npc edit.");

        var rolledWith = option.Modifier is { } mod ? $"{option.Name} {NpcSheet.Signed(mod)}" : option.Dice;
        if (bonus != 0)
            rolledWith += $", Bonus {NpcSheet.Signed(bonus)}";

        // Only 2d6 rolls have hit bands; a damage roll is just a number
        var outcome = option.Modifier is null ? null : MoveRoller.OutcomeOf(roll);
        List<IEmbedField> fields = [new EmbedField("Rolled with", rolledWith)];
        if (outcome is { } band)
            fields.Add(new EmbedField(MoveRoller.Describe(band), MoveRoller.GeneralMeaning(band)));

        // Only a damage roll hurts the target; a 2d6 roll is just aimed at it
        string? aimedAt = null;
        string? result = null;
        if (!string.IsNullOrWhiteSpace(target))
        {
            if (outcome is null)
            {
                var hit = await EnemyHit.ApplyAsync(encounterService, target, roll.Value);
                fields.Add(hit.Field);
                aimedAt = hit.Target;
                result = hit.Result;
            }
            else
            {
                aimedAt = (await encounterService.FindEnemyAsync(target))?.Name ?? target.Trim();
                fields.Add(new EmbedField("🎯 Target", aimedAt));
            }
        }

        // Not recorded in roll stats, which belong to the player who rolled
        var reply = await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"🎲 {npc.Name}: {option.Name}",
                Description: DiceHelper.BuildDiceResult(roll),
                Fields: fields,
                Colour: outcome is { } o ? MoveRoller.ColourFor(o) : _feedbackService.Theme.Success
            ),
            ct: CancellationToken
        );

        // Does nothing between fights
        if (reply.IsSuccess)
        {
            await encounterService.LogAsync(new EncounterAction
            {
                Actor = npc.Name,
                NpcID = npc.ID,
                Action = option.Name,
                Roll = outcome is { } rolled ? $"{roll.Value} · {MoveRoller.Describe(rolled)}" : $"{dice} = {roll.Value}",
                Target = aimedAt,
                Result = result,
            });
        }

        return reply;
    }

    public static string NotFound(string name) => $"No NPC called {name} is with the party. See them all with /npc list.";
}
