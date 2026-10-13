using System.ComponentModel;
using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Debilities;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Moves;
using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("move")]
public class MoveCommand : CommandGroup
{
    private const string BonusDescription = "Any +forward, ongoing bonus, or penalty to add to the roll";

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiceRoller _diceRoller;
    private readonly IResourceService _resourceService;
    private readonly IRollStatsService _rollStatsService;

    public MoveCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiceRoller diceRoller,
        IResourceService resourceService,
        IRollStatsService rollStatsService)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _diceRoller = diceRoller;
        _resourceService = resourceService;
        _rollStatsService = rollStatsService;
    }

    [Command("hack-and-slash")]
    [Description("Attack an enemy in melee (+STR)")]
    public Task<IResult> HackAndSlashAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.HackAndSlash, StatType.Str, bonus);

    [Command("volley")]
    [Description("Take aim and shoot at an enemy at range (+DEX)")]
    public Task<IResult> VolleyAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.Volley, StatType.Dex, bonus);

    [Command("defy-danger")]
    [Description("Act despite an imminent threat (+the stat that fits how you do it)")]
    public Task<IResult> DefyDangerAsync(
        [Description("STR: power through, DEX: dodge, CON: endure, INT: think fast, WIS: resist, CHA: charm")]
        StatType stat,
        [Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.DefyDanger, stat, bonus);

    [Command("defend")]
    [Description("Stand in defense of a person, item, or location (+CON)")]
    public Task<IResult> DefendAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.Defend, StatType.Con, bonus);

    [Command("spout-lore")]
    [Description("Consult your accumulated knowledge about something (+INT)")]
    public Task<IResult> SpoutLoreAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.SpoutLore, StatType.Int, bonus);

    [Command("discern-realities")]
    [Description("Closely study a situation or person (+WIS)")]
    public Task<IResult> DiscernRealitiesAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.DiscernRealities, StatType.Wis, bonus);

    [Command("parley")]
    [Description("Use leverage to manipulate a GM character (+CHA)")]
    public Task<IResult> ParleyAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.Parley, StatType.Cha, bonus);

    [Command("aid")]
    [Description("Help or hinder another player's character (+bonds with them)")]
    public async Task<IResult> AidOrInterfereAsync(
        [Description("The player you're helping or hindering")]
        IUser user,
        [Description(BonusDescription)] int bonus = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        var bonds = character.Bonds?.Count(b => b.TargetID == user.ID) ?? 0;
        return await ReplyWithMoveAsync(character, BasicMoves.AidOrInterfere, $"Bond with {user.Username}", bonds, bonus);
    }

    [Command("last-breath")]
    [Description("You're dying. Roll to see what lies beyond the Black Gates (+nothing)")]
    public async Task<IResult> LastBreathAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        return await ReplyWithMoveAsync(character, BasicMoves.LastBreath, modifierLabel: null, modifier: 0, bonus: 0);
    }

    [Command("class")]
    [Description("Roll one of your class moves, like Channel or Raise the Dead")]
    public async Task<IResult> ClassMoveAsync(
        [Description("The class move to roll")]
        [AutocompleteProvider(RolledClassMoveAutocomplete.ID)]
        string move,
        [Description(BonusDescription)] int bonus = 0,
        [Description("Roll with a different stat than usual, if a move lets you")]
        StatType? stat = null)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return await ReplyWithErrorAsync($"The {character.Class.Type} doesn't have a class sheet in the bot yet.");

        var classMove = sheet.FindMove(move);
        var rollsFrom = sheet;
        if (classMove is null
            && Multiclassing.KnownForeignMoves(character, sheet).FirstOrDefault(f => f.Move.Name.Equals(move.Trim(), StringComparison.OrdinalIgnoreCase)) is { } foreign)
        {
            // A move learned from another class's sheet
            classMove = foreign.Move;
            rollsFrom = foreign.Sheet;
        }

        if (classMove?.Roll is not { } definition)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have a rolled class move called {move}.");

        if (!ClassSheetLibrary.KnownMoves(character).Contains(classMove.Name, StringComparer.OrdinalIgnoreCase))
            return await ReplyWithErrorAsync($"{character.Name} doesn't know {classMove.Name}. Learn it with /class learn");

        if (stat is null && classMove.RollsWith is { } rollsWith)
        {
            if (rollsFrom.FindResource(rollsWith) is { } resource)
            {
                var value = _resourceService.GetValue(character, resource);
                return await ReplyWithMoveAsync(character, definition, resource.Name, value, bonus);
            }

            if (rollsFrom.ClassStat?.Name.Equals(rollsWith, StringComparison.OrdinalIgnoreCase) == true)
            {
                stat = rollsFrom == sheet ? ClassSheetLibrary.ResolveClassStat(character, sheet) : null;
                if (stat is null)
                {
                    return await ReplyWithErrorAsync(rollsFrom == sheet
                        ? $"Choose your {sheet.ClassStat.Feature} with /class learn first, it decides your {rollsWith} stat."
                        : $"{classMove.Name} rolls the {rollsFrom.Type}'s {rollsWith} stat. Pick which stat to use with the stat option.");
                }
            }
        }

        if ((stat ?? definition.Stat) is not { } statType)
            return await ReplyWithMoveAsync(character, definition, modifierLabel: null, modifier: 0, bonus);

        var characterStat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (characterStat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        return await ReplyWithMoveAsync(character, definition, statType.ToString().ToUpper(), characterStat.Modifier, bonus, statType);
    }

    private async Task<IResult> MakeStatMoveAsync(MoveDefinition move, StatType statType, int bonus)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        return await ReplyWithMoveAsync(character, move, statType.ToString().ToUpper(), stat.Modifier, bonus, statType);
    }

    /// <param name="stat">The stat being rolled, so its debility (if any) applies.</param>
    private async Task<IResult> ReplyWithMoveAsync(Character character, MoveDefinition move, string? modifierLabel, int modifier, int bonus, StatType? stat = null)
    {
        var debility = stat is { } rolled ? DebilityRules.RollPenalty(character, rolled) : null;
        var roll = MoveRoller.Roll(_diceRoller, modifier + bonus + (debility?.Penalty ?? 0));

        var fields = new List<IEmbedField>();

        if (modifierLabel is not null || bonus != 0 || debility is not null)
        {
            var parts = new List<string>();
            if (modifierLabel is not null)
                parts.Add($"{modifierLabel} {FormatSigned(modifier)}");
            if (debility is { } penalty)
                parts.Add($"{penalty.Debility} {FormatSigned(penalty.Penalty)}");
            if (bonus != 0)
                parts.Add($"Bonus {FormatSigned(bonus)}");

            fields.Add(new EmbedField("Rolled with", string.Join(", ", parts), IsInline: true));
        }

        fields.Add(new EmbedField(MoveRoller.Describe(roll.Outcome), move.Describe(roll.Outcome)));

        var isHit = roll.Outcome != MoveOutcome.Miss;
        if (move.ShowsOptions(roll.Outcome))
        {
            fields.Add(new EmbedField("Options", string.Join('\n', move.Options!.Select(o => $"• {o}"))));
        }

        if (isHit && move.RollsDamage)
        {
            fields.Add(RollDamage(character));
        }

        var colour = MoveRoller.ColourFor(roll.Outcome);

        var reply = await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"🎲 {character.Name}: {move.Name}",
                Description: $"*{move.Trigger}...*\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
                Fields: fields,
                Colour: colour
            ),
            ct: CancellationToken
        );

        if (reply.IsSuccess && _context.TryGetChannelID(out var channelID))
            await _rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, roll.Dice);

        return reply;
    }

    private IEmbedField RollDamage(Character character)
    {
        var expression = DiceHelper.AddModifier(character.DamageModifier ?? character.Class.Damage, character.DamageRating);

        return _diceRoller.TryRoll(expression, out var damage)
            ? new EmbedField("Damage", $"{expression} → **{damage.Value}**")
            : new EmbedField("Damage", $"Couldn't roll `{expression}`. Fix it with /character damage-modifier-change");
    }

    private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private async Task<Character?> GetOwnCharacterAsync()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

        return await _characterService.GetCharacterFromUserAsync(userId);
    }

    private async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync(error, ct: CancellationToken);
    }
}
