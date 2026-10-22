using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.GameMaster;

public partial class DmCommand
{
    /// <summary>
    /// Running a fight: starting it, putting monsters in, hurting them, and ending it.
    /// </summary>
    [Group("encounter")]
    public class EncounterCommands : DungeonWorldCommandBase
    {
        private const string EnemyDescription = "The enemy";
        private const string NoFight = "No fight is going on. Start one with /dm encounter start.";

        private readonly IEncounterService _encounterService;
        private readonly IArmorService _armorService;
        private readonly IDiceRoller _diceRoller;
        private readonly IGameMasterService _gameMaster;
        private readonly SceneDirectory _scenes;

        public EncounterCommands(
            ICommandContext context,
            FeedbackService feedbackService,
            IEncounterService encounterService,
            IArmorService armorService,
            IDiceRoller diceRoller,
            IGameMasterService gameMaster,
            SceneDirectory scenes)
            : base(context, feedbackService)
        {
            _encounterService = encounterService;
            _armorService = armorService;
            _diceRoller = diceRoller;
            _gameMaster = gameMaster;
            _scenes = scenes;
        }

        [Command("start")]
        [Description("Start a fight. Add monsters to it with /dm encounter add, or on its scene")]
        public async Task<IResult> StartAsync(
            [Description("What to call the fight, e.g. Goblin ambush")]
            string name,
            [Description("The dashboard's encounter scene it's fought on, linked in the announcement")]
            [AutocompleteProvider(SceneAutocomplete.ID)]
            string? scene = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (Text(name) is not { } trimmed)
                return await ReplyWithErrorAsync("Give the fight a name.");

            SceneLink? link = null;
            if (Text(scene) is { } wanted && (link = await _scenes.FindAsync(wanted)) is null)
                return await ReplyWithErrorAsync($"There's no encounter scene called {wanted} on the dashboard.");

            // This reply is the announcement, so the bot doesn't make another
            if (await _encounterService.StartAsync(trimmed, link, announced: true) is not { } encounter)
            {
                var current = await _encounterService.GetActiveAsync();
                return await ReplyWithErrorAsync($"{current?.Name ?? "A fight"} is still going. End it with /dm encounter end first.");
            }

            return await _feedbackService.SendContextualEmbedAsync(EncounterAnnouncements.Started(encounter), ct: CancellationToken);
        }

        [Command("end")]
        [Description("End the fight, and remove everyone's temporary armor that lasted for it")]
        public async Task<IResult> EndAsync()
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.EndAsync(announced: true) is not { } encounter)
                return await ReplyWithErrorAsync(NoFight);

            var cleared = await _armorService.EndFightForAllAsync();

            return await _feedbackService.SendContextualEmbedAsync(
                EncounterAnnouncements.Ended(encounter, ArmorCommand.DescribeCleared(cleared)), ct: CancellationToken);
        }

        [Command("add")]
        [Description("Put saved monsters into the fight. Save them first with /dm monster save")]
        public async Task<IResult> AddAsync(
            [Description("The monster")]
            [AutocompleteProvider(MonsterAutocomplete.ID)]
            string monster,
            [Description("How many")]
            [MinValue(1)]
            [MaxValue(20)]
            int count = 1)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.GetActiveAsync() is not { } encounter)
                return await ReplyWithErrorAsync(NoFight);

            if (await _encounterService.FindMonsterAsync(monster) is not { } template)
                return await ReplyWithErrorAsync($"There's no saved monster called {monster}. See /dm monster list");

            var spawned = await _encounterService.SpawnAsync(encounter, template, count);

            return await ReplyAsync("Enemies Appear!",
                $"{string.Join(", ", spawned.Select(e => $"**{e.Name}**"))} joined **{encounter.Name}**.\n{EnemySheet.Summary(template)}");
        }

        [Command("add-custom")]
        [Description("Put a one-off enemy into the fight, without saving it as a monster")]
        public async Task<IResult> AddCustomAsync(
            [Description("Its name")]
            string name,
            [Description("Its max HP")]
            [Option("max-hp")]
            [MinValue(1)]
            int maxHp,
            [Description("Armor against physical damage")]
            [MinValue(0)]
            int armor = 0,
            [Description("Dice it rolls for damage, e.g. d8+1")]
            string? damage = null,
            [Description("Its tags, e.g. Horde, Small, Intelligent")]
            string? tags = null,
            [Description("What it wants, e.g. To overwhelm")]
            string? instinct = null,
            [Description("Its moves, separated by semicolons")]
            string? moves = null,
            [Description("Anything else worth remembering")]
            string? notes = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.GetActiveAsync() is not { } encounter)
                return await ReplyWithErrorAsync(NoFight);

            if (Text(name) is not { } trimmed)
                return await ReplyWithErrorAsync("Give the enemy a name.");
            if (await _encounterService.FindEnemyAsync(trimmed) is { } existing)
                return await ReplyWithErrorAsync($"{existing.Name} is already in the fight.");

            var enemy = new Enemy
            {
                Name = trimmed,
                CurrentHP = maxHp,
                MaxHP = maxHp,
                Armor = armor,
                Damage = Text(damage),
                Tags = Text(tags),
                Instinct = Text(instinct),
                Moves = Text(moves),
                Notes = Text(notes),
            };
            await _encounterService.AddEnemyAsync(encounter, enemy);

            return await ReplyAsync("An Enemy Appears!", $"**{enemy.Name}** joined **{encounter.Name}**.\n{EnemySheet.Summary(enemy)}");
        }

        [Command("hurt")]
        [Description("Damage an enemy, reduced by its armor if it's physical")]
        public async Task<IResult> HurtAsync(
            [Description(EnemyDescription)]
            [AutocompleteProvider(EnemyAutocomplete.ID)]
            string enemy,
            [Description("Amount to damage, before armor. Can be a dice roll or just flat damage")]
            string damage,
            [Description("Physical damage is stopped by its armor; anything else goes straight through")]
            DamageType type = DamageType.Physical,
            [Description("How much of its armor the hit ignores")]
            [MinValue(0)]
            int piercing = 0)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindEnemyAsync(enemy) is not { } target)
                return await ReplyWithErrorAsync(NotFound(enemy));

            if (!_diceRoller.TryRoll(damage, out var roll))
                return await ReplyWithErrorAsync($"`{damage}` is not a valid amount or dice roll.");

            var hit = await _encounterService.DamageAsync(target, roll.Value, type, piercing);

            return await ReplyAsync("Hit!", DescribeHit(target, hit));
        }

        [Command("heal")]
        [Description("Heal an enemy")]
        public async Task<IResult> HealAsync(
            [Description(EnemyDescription)]
            [AutocompleteProvider(EnemyAutocomplete.ID)]
            string enemy,
            [Description("Amount to heal. Can be a dice roll or left blank to full heal")]
            string? heal = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindEnemyAsync(enemy) is not { } target)
                return await ReplyWithErrorAsync(NotFound(enemy));

            if (string.IsNullOrWhiteSpace(heal))
            {
                await _encounterService.HealAsync(target);
            }
            else
            {
                if (!_diceRoller.TryRoll(heal, out var roll))
                    return await ReplyWithErrorAsync($"`{heal}` is not a valid amount or dice roll.");

                await _encounterService.HealAsync(target, roll.Value);
            }

            return await ReplyAsync("Healed!", $"{target.Name} is at {target.CurrentHP}/{target.MaxHP} HP · {EnemySheet.Condition(target)}");
        }

        [Command("edit")]
        [Description("Change an enemy in the fight. Only what you fill in changes")]
        public async Task<IResult> EditAsync(
            [Description(EnemyDescription)]
            [AutocompleteProvider(EnemyAutocomplete.ID)]
            string enemy,
            [Description("A new name")]
            string? rename = null,
            [Description("Its max HP. Its current HP is kept, up to the new max")]
            [Option("max-hp")]
            [MinValue(1)]
            int? maxHp = null,
            [Description("Set its current HP")]
            [MinValue(0)]
            int? hp = null,
            [Description("Armor against physical damage")]
            [MinValue(0)]
            int? armor = null,
            [Description("Dice it rolls for damage. " + ClearHint)]
            string? damage = null,
            [Description("Its tags. " + ClearHint)]
            string? tags = null,
            [Description("What it wants. " + ClearHint)]
            string? instinct = null,
            [Description("Its moves, separated by semicolons. " + ClearHint)]
            string? moves = null,
            [Description("Anything else worth remembering. " + ClearHint)]
            string? notes = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindEnemyAsync(enemy) is not { } target)
                return await ReplyWithErrorAsync(NotFound(enemy));

            if (Text(rename) is { } newName && !newName.Equals(target.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await _encounterService.FindEnemyAsync(newName) is { } taken)
                    return await ReplyWithErrorAsync($"{taken.Name} is already in the fight.");
            }

            if (Text(rename) is { } name)
                target.Name = name;
            if (maxHp is { } max)
                target.MaxHP = max;
            if (hp is { } current)
                target.CurrentHP = current;
            if (armor is { } newArmor)
                target.Armor = newArmor;

            target.Damage = Change(target.Damage, damage);
            target.Tags = Change(target.Tags, tags);
            target.Instinct = Change(target.Instinct, instinct);
            target.Moves = Change(target.Moves, moves);
            target.Notes = Change(target.Notes, notes);

            await _encounterService.SaveAsync(target);

            return await ReplyAsync("Enemy Updated!", $"**{target.Name}**\n{EnemySheet.Summary(target)}");
        }

        [Command("remove")]
        [Description("Take an enemy out of the fight: it fled, or it was never really there")]
        public async Task<IResult> RemoveAsync(
            [Description(EnemyDescription)]
            [AutocompleteProvider(EnemyAutocomplete.ID)]
            string enemy)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindEnemyAsync(enemy) is not { } target)
                return await ReplyWithErrorAsync(NotFound(enemy));

            await _encounterService.RemoveAsync(target);

            return await ReplyAsync("Enemy Removed", $"{target.Name} is out of the fight.");
        }

        [Command("show")]
        [Description("See every enemy's HP, instinct and moves. Only you see the reply")]
        public async Task<IResult> ShowAsync()
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.GetActiveAsync() is not { } encounter)
                return await ReplyWithErrorAsync(NoFight);

            var embed = new Embed(
                Title: $"⚔️ {encounter.Name}",
                Description: encounter.Enemies.Count == 0 ? "No enemies yet. Add some with /dm encounter add." : string.Empty,
                Fields: EnemySheet.GameMasterFields(encounter).ToList(),
                Colour: _feedbackService.Theme.Primary);

            return await _feedbackService.SendContextualEmbedAsync(
                embed, new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral), CancellationToken);
        }

        /// <summary>
        /// e.g. "Goblin 2 took 4 damage! (0/3 HP) · 💀 Defeated" with the armor's share under it.
        /// </summary>
        public static string DescribeHit(Enemy enemy, ArmoredHit hit)
        {
            var description = $"{enemy.Name} took {hit.Taken} damage! ({enemy.CurrentHP}/{enemy.MaxHP} HP) · {EnemySheet.Condition(enemy)}";
            if (hit.Blocked > 0)
                description += $"\nRolled {hit.Rolled}, armor stopped {hit.Blocked}";
            return description;
        }

        public static string NotFound(string name) => $"There's no enemy called {name} in the fight. See /dm encounter show";

        private async Task<Result> ReplyNotGameMasterAsync() =>
            await ReplyWithErrorAsync($"Only someone with the {_gameMaster.RoleName} role can do that.");
    }
}
