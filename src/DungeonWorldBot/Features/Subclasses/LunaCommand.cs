using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses.Homebrew;
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

namespace DungeonWorldBot.Features.Subclasses;

public enum VowEnding
{
    Completed,
    Abandoned,
    Impossible,
}

public partial class SubclassCommand
{
    /// <summary>
    /// The Champion of Luna's moves that need more than a plain roll. Revelation is rolled with /move class.
    /// </summary>
    [Group("luna")]
    public class LunaCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IAbilityService abilityService,
        IDiceRoller diceRoller,
        IRollStatsService rollStatsService,
        IDamageService damageService)
        : DungeonWorldCommandBase(context, feedbackService)
    {
        private const string BonusDescription = "Any +forward, ongoing bonus, or penalty to add to the roll";

        [Command("vow")]
        [Description("Declare an objective in Luna's name and choose a Boon of Luna")]
        public async Task<IResult> VowAsync(
            [Description("Wrath: +damage, Aptitude: +a stat, Veil: touch magic, Fortune: +Last Breath")]
            LunaBoon boon,
            [Description("What you swear to do in Luna's name")]
            string objective,
            [Description("For a Boon of Aptitude: the stat it raises")]
            StatType? stat = null)
        {
            var (character, error) = await GetChampionAsync(ChampionOfLuna.LunasVow);
            if (character is null)
                return await ReplyWithErrorAsync(error!);

            if (ChampionOfLuna.ActiveVowOf(character) is { } current)
                return await ReplyWithErrorAsync($"Only one Vow can be active at a time. You're sworn to: {current.Objective}\nEnd it with /subclass luna vow-end");

            if (boon == LunaBoon.Aptitude && stat is null)
                return await ReplyWithErrorAsync("Choose the stat a Boon of Aptitude raises with the stat option.");

            var vow = new LunaVow(boon, boon == LunaBoon.Aptitude ? stat : null, objective.Trim());
            await abilityService.AddAbilityAsync(character, ChampionOfLuna.ActiveVow, ChampionOfLuna.Store(vow), null);

            var (champion, echo) = ChampionOfLuna.Describe(vow);
            return await ReplyAsync(
                $"🌕 {character.Name} swears a Vow to Luna",
                $"*{vow.Objective}*",
                [
                    new EmbedField(vow.BoonName, champion),
                    new EmbedField("Echo (the rest of the party)", echo),
                ]);
        }

        [Command("vow-end")]
        [Description("End your Vow: the objective was completed, abandoned, or became impossible")]
        public async Task<IResult> EndVowAsync(
            [Description("How the Vow ended")]
            VowEnding ending)
        {
            var (character, error) = await GetChampionAsync(ChampionOfLuna.LunasVow);
            if (character is null)
                return await ReplyWithErrorAsync(error!);

            if (abilityService.FindAbility(character, ChampionOfLuna.ActiveVow) is not { } ability)
                return await ReplyWithErrorAsync($"{character.Name} has no active Vow.");

            var vow = ChampionOfLuna.ActiveVowOf(character);
            await abilityService.RemoveAbilityAsync(character, ability);

            var how = ending switch
            {
                VowEnding.Completed => "is fulfilled",
                VowEnding.Abandoned => "is abandoned",
                _ => "can no longer be kept",
            };
            return await ReplyAsync(
                $"🌑 {character.Name}'s Vow {how}",
                vow is not null ? $"*{vow.Objective}*\nThe {vow.BoonName} fades." : "The Boon fades.");
        }

        [Command("blood-moon")]
        [Description("Sacrifice HP to empower an attack that hit, then roll +CHA to recover it")]
        public async Task<IResult> BloodMoonAsync(
            [Description("How much of your own HP to sacrifice")]
            [MinValue(0)]
            int sacrifice,
            [Description("The damage your attack already rolled. Leave blank to roll your damage now.")]
            [MinValue(0)]
            int? damage = null,
            [Description("A willing party member who also sacrifices HP")]
            IUser? ally = null,
            [Option("ally-sacrifice")]
            [Description("How much HP the ally sacrifices")]
            [MinValue(0)]
            int allySacrifice = 0,
            [Description(BonusDescription)] int bonus = 0,
            [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        {
            var (character, error) = await GetChampionAsync(ChampionOfLuna.BloodMoon);
            if (character is null)
                return await ReplyWithErrorAsync(error!);

            List<(Character Character, int HP)> offerings = [];
            if (sacrifice > 0)
                offerings.Add((character, sacrifice));

            if (ally is not null && allySacrifice > 0)
            {
                var allyCharacter = await characterService.GetCharacterFromUserAsync(ally.ID);
                if (allyCharacter is null)
                    return await ReplyWithErrorAsync($"{ally.Username} does not have a character.");
                if (allyCharacter.ID == character.ID)
                    return await ReplyWithErrorAsync("Put your own sacrifice in the sacrifice option.");

                offerings.Add((allyCharacter, allySacrifice));
            }

            if (offerings.Count == 0)
                return await ReplyWithErrorAsync("Luna needs an offering: sacrifice at least 1 HP.");

            if (offerings.FirstOrDefault(o => o.HP > o.Character.Health.CurrentHP) is { Character: not null } tooMuch)
                return await ReplyWithErrorAsync($"{tooMuch.Character.Name} only has {tooMuch.Character.Health.CurrentHP} HP to give.");

            var totalSacrificed = offerings.Sum(o => o.HP);
            List<IEmbedField> fields =
            [
                new EmbedField("Offered to Luna", string.Join('\n', offerings.Select(o => $"{o.Character.Name}: {o.HP} HP"))),
            ];

            // The empowered strike
            string damageText;
            if (damage is { } dealt)
            {
                var total = Math.Max(0, dealt + extraDamage) + totalSacrificed;
                damageText = extraDamage != 0
                    ? $"{dealt} {FormatSigned(extraDamage)} extra + {totalSacrificed} sacrificed → **{total}**"
                    : $"{dealt} + {totalSacrificed} sacrificed → **{total}**";
            }
            else
            {
                if (await damageService.RollAsync(character, ChampionOfLuna.BloodMoon, extraDamage) is not { } rolled)
                    return await ReplyWithErrorAsync("Couldn't roll your damage. Fix it with /character damage-dice");

                damageText = $"{rolled.Expression} → {rolled.Total} + {totalSacrificed} sacrificed → **{rolled.Total + totalSacrificed}**" + rolled.Details;
            }

            fields.Add(new EmbedField("Damage", damageText));

            foreach (var (who, hp) in offerings)
                await characterService.DamageAsync(who, hp);

            // Then Luna decides how much comes back
            var (roll, rolledWith) = await RollStatAsync(character, StatType.Cha, bonus);
            fields.Insert(0, new EmbedField("Rolled with", rolledWith, IsInline: true));

            fields.Add(new EmbedField(MoveRoller.Describe(roll.Outcome), roll.Outcome switch
            {
                MoveOutcome.StrongHit => "Luna is pleased. All HP sacrificed is recovered.",
                MoveOutcome.WeakHit => "Luna takes her share. Half of the HP sacrificed is recovered, rounded down.",
                _ => "Luna keeps it all. None of the HP is recovered, and the GM makes a move.",
            }));

            List<string> recovered = [];
            foreach (var (who, hp) in offerings)
            {
                var back = ChampionOfLuna.Recovered(hp, roll.Outcome);
                if (back > 0)
                    await characterService.HealAsync(who, back);

                recovered.Add($"{who.Name}: +{back} HP ({who.Health.CurrentHP}/{who.Health.MaxHP})" +
                              (who.Health.CurrentHP == 0 ? " · at 0 HP, roll /move last-breath" : string.Empty));
            }

            fields.Add(new EmbedField("Recovered", string.Join('\n', recovered)));

            return await ReplyWithRollAsync(character, $"🩸 {character.Name}: {ChampionOfLuna.BloodMoon}",
                "*Life offered to Luna empowers the strike...*", roll, fields);
        }

        [Command("lunar-barrage")]
        [Description("Channel Luna into your bow: exploding d10 damage to up to 3 enemies (+DEX)")]
        public async Task<IResult> LunarBarrageAsync(
            [Description(BonusDescription)] int bonus = 0,
            [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        {
            var (character, error) = await GetChampionAsync(ChampionOfLuna.LunarBarrage);
            if (character is null)
                return await ReplyWithErrorAsync(error!);

            // The damage builds before the arrow is released
            var dice = ChampionOfLuna.RollBarrage(() => diceRoller.Roll("1d10").Value);
            var damageBonuses = await damageService.DamageBonusesAsync(character, extraDamage);
            var gear = await damageService.ApplyGearAsync(character, ChampionOfLuna.LunarBarrage, dice.Select(d => (10, d)).ToList());
            var flat = damageBonuses.Sum(b => b.Amount) + gear.Bonus;
            var total = Math.Max(0, dice.Sum() + flat);

            var (roll, rolledWith) = await RollStatAsync(character, StatType.Dex, bonus);
            var targets = ChampionOfLuna.BarrageTargets(roll.Outcome);

            var damageText = $"d10s: {string.Join(" → ", dice)}" +
                             (dice.Count > 1 ? $" ({dice.Count - 1} build{(dice.Count == 2 ? "" : "s")})" : string.Empty) +
                             (flat != 0 ? $"\n{dice.Sum()} {(flat > 0 ? "+" : "-")} {Math.Abs(flat)}" : string.Empty) +
                             $"\n**{total}** to each target" + Includes(damageBonuses) + string.Concat(gear.Notes.Select(n => $"\n{n}"));

            List<IEmbedField> fields =
            [
                new EmbedField("Rolled with", rolledWith, IsInline: true),
                new EmbedField(MoveRoller.Describe(roll.Outcome),
                    $"Target up to {targets} enem{(targets == 1 ? "y" : "ies")}." + (roll.Outcome == MoveOutcome.Miss ? " The GM makes a move." : string.Empty)),
                new EmbedField("Damage", damageText),
            ];

            return await ReplyWithRollAsync(character, $"🌙 {character.Name}: {ChampionOfLuna.LunarBarrage}",
                "*Luna's power builds within the arrow...*", roll, fields);
        }

        [Command("life-for-life")]
        [Description("Trade your life for an enemy's: they die, you drop to 0 HP and roll Last Breath")]
        public async Task<IResult> LifeForLifeAsync(
            [Option("enemy-level")]
            [Description("The enemy's level. It can't be higher than yours.")]
            [MinValue(0)]
            int enemyLevel)
        {
            var (character, error) = await GetChampionAsync(ChampionOfLuna.LifeForLife);
            if (character is null)
                return await ReplyWithErrorAsync(error!);

            if (enemyLevel > character.Level)
                return await ReplyWithErrorAsync($"Luna won't trade for an enemy above your level ({character.Level}).");

            await characterService.DamageAsync(character, character.Health.CurrentHP);

            var bonuses = await BonusesAsync(character, RollKind.LastBreath);
            var roll = MoveRoller.Roll(diceRoller, bonuses.Sum(b => b.Amount));
            var lastBreath = BasicMoves.LastBreath;

            List<IEmbedField> fields = [];
            if (bonuses.Count > 0)
                fields.Add(new EmbedField("Rolled with", string.Join(", ", bonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}")), IsInline: true));
            fields.Add(new EmbedField("The enemy", "is instantly killed. No damage is rolled."));
            fields.Add(new EmbedField($"Last Breath: {MoveRoller.Describe(roll.Outcome)}", lastBreath.Describe(roll.Outcome)));

            return await ReplyWithRollAsync(character, $"🌑 {character.Name}: {ChampionOfLuna.LifeForLife}",
                $"*{character.Name} offers their life to Luna in exchange for their foe's, and falls to 0 HP...*", roll, fields);
        }

        /// <summary>
        /// Rolls 2d6 + a stat, with its debility and any subclass bonuses.
        /// </summary>
        /// <returns>The roll, and what went into it for the "Rolled with" field.</returns>
        private async Task<(MoveRoll Roll, string RolledWith)> RollStatAsync(Character character, StatType stat, int bonus)
        {
            var modifier = character.Stats.FirstOrDefault(s => s.StatType == stat)?.Modifier ?? 0;
            var debility = DebilityRules.RollPenalty(character, stat);
            var bonuses = await BonusesAsync(character, RollKind.Stat, stat);

            var parts = new List<string> { $"{stat.ToString().ToUpper()} {FormatSigned(modifier)}" };
            if (debility is { } penalty)
                parts.Add($"{penalty.Debility} {FormatSigned(penalty.Penalty)}");
            parts.AddRange(bonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}"));
            if (bonus != 0)
                parts.Add($"Bonus {FormatSigned(bonus)}");

            var roll = MoveRoller.Roll(diceRoller, modifier + (debility?.Penalty ?? 0) + bonuses.Sum(b => b.Amount) + bonus);
            return (roll, string.Join(", ", parts));
        }

        private Task<IReadOnlyList<RollBonus>> BonusesAsync(Character character, RollKind kind, StatType? stat = null) =>
            damageService.BonusesAsync(character, kind, stat);

        private static string Includes(IReadOnlyList<RollBonus> bonuses) =>
            bonuses.Count > 0 ? $"\nIncludes {string.Join(", ", bonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}"))}" : string.Empty;

        private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();

        /// <returns>The user's character if they're a Champion of Luna who knows the move, or why not.</returns>
        private async Task<(Character? Character, string? Error)> GetChampionAsync(string move)
        {
            var userId = GetUserId();

            var character = await characterService.GetCharacterFromUserAsync(userId);
            if (character is null)
                return (null, "No character exists. Try making one with /character create");

            if (SubclassLibrary.Of(character) != ChampionOfLuna.Subclass)
                return (null, $"{character.Name} isn't a {ChampionOfLuna.Name}. See /subclass info {ChampionOfLuna.Name}");

            if (abilityService.FindAbility(character, move) is null)
            {
                var level = ChampionOfLuna.Subclass.Moves.FirstOrDefault(m => m.Move.Name == move)?.Level;
                return (null, $"{character.Name} hasn't unlocked {move} yet" + (level is not null ? $" (level {level})." : "."));
            }

            return (character, null);
        }

        private async Task<IResult> ReplyWithRollAsync(Character character, string title, string flavour, MoveRoll roll, IReadOnlyList<IEmbedField> fields)
        {
            var embed = new Embed(
                Title: title,
                Description: $"{flavour}\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
                Fields: fields.ToList(),
                Colour: MoveRoller.ColourFor(roll.Outcome)
            );

            var reply = await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);

            if (reply.IsSuccess && _context.TryGetChannelID(out var channelID))
                await rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, roll.Dice, RollLabel.Of(embed));

            return reply;
        }
    }
}
