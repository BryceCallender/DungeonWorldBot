using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.GameMaster;

public partial class DmCommand
{
    /// <summary>
    /// Monsters written up once, to put into fights with /dm encounter add.
    /// </summary>
    [Group("monster")]
    public class MonsterCommands : DungeonWorldCommandBase
    {
        private const string MonsterDescription = "The monster";

        private readonly IEncounterService _encounterService;
        private readonly IGameMasterService _gameMaster;

        public MonsterCommands(ICommandContext context, FeedbackService feedbackService, IEncounterService encounterService, IGameMasterService gameMaster)
            : base(context, feedbackService)
        {
            _encounterService = encounterService;
            _gameMaster = gameMaster;
        }

        [Command("save")]
        [Description("Write up a monster, or change one already saved. Only what you fill in changes on an existing one")]
        public async Task<IResult> SaveAsync(
            [Description("Its name, e.g. Goblin")]
            [AutocompleteProvider(MonsterAutocomplete.ID)]
            string name,
            [Description("Its max HP. Needed for a new monster")]
            [Option("max-hp")]
            [MinValue(1)]
            int? maxHp = null,
            [Description("Armor against physical damage")]
            [MinValue(0)]
            int? armor = null,
            [Description("Dice it rolls for damage, e.g. d8+1. " + ClearHint)]
            string? damage = null,
            [Description("Its tags, e.g. Horde, Small, Intelligent. " + ClearHint)]
            string? tags = null,
            [Description("What it wants, e.g. To overwhelm. " + ClearHint)]
            string? instinct = null,
            [Description("Its moves, separated by semicolons. " + ClearHint)]
            string? moves = null,
            [Description("Anything else worth remembering. " + ClearHint)]
            string? notes = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (Text(name) is not { } trimmed)
                return await ReplyWithErrorAsync("Give the monster a name.");

            var monster = await _encounterService.FindMonsterAsync(trimmed);
            var isNew = monster is null;
            if (monster is null)
            {
                if (maxHp is null)
                    return await ReplyWithErrorAsync($"{trimmed} is a new monster, so it needs its max HP.");
                monster = new MonsterTemplate { Name = trimmed };
            }

            if (maxHp is { } max)
                monster.MaxHP = max;
            if (armor is { } newArmor)
                monster.Armor = newArmor;

            monster.Damage = Change(monster.Damage, damage);
            monster.Tags = Change(monster.Tags, tags);
            monster.Instinct = Change(monster.Instinct, instinct);
            monster.Moves = Change(monster.Moves, moves);
            monster.Notes = Change(monster.Notes, notes);

            await _encounterService.SaveMonsterAsync(monster);

            return await ReplyAsync(isNew ? "Monster Saved!" : "Monster Updated!", monster.Name, EnemySheet.Fields(monster));
        }

        [Command("from-enemy")]
        [Description("Save an enemy in the fight as a monster, to put into later fights")]
        public async Task<IResult> FromEnemyAsync(
            [Description("The enemy")]
            [AutocompleteProvider(EnemyAutocomplete.ID)]
            string enemy,
            [Description("What to save it as, if not its name in the fight")]
            string? name = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindEnemyAsync(enemy) is not { } source)
                return await ReplyWithErrorAsync(EncounterCommands.NotFound(enemy));

            var saveAs = Text(name) ?? source.Name;
            if (await _encounterService.FindMonsterAsync(saveAs) is { } existing)
                return await ReplyWithErrorAsync($"{existing.Name} is already saved. Change it with /dm monster save, or pick another name.");

            var monster = new MonsterTemplate
            {
                Name = saveAs,
                MaxHP = source.MaxHP,
                Armor = source.Armor,
                Damage = source.Damage,
                Tags = source.Tags,
                Instinct = source.Instinct,
                Moves = source.Moves,
                Notes = source.Notes,
            };
            await _encounterService.SaveMonsterAsync(monster);

            return await ReplyAsync("Monster Saved!", monster.Name, EnemySheet.Fields(monster));
        }

        [Command("list")]
        [Description("List the saved monsters")]
        public async Task<IResult> ListAsync()
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            var monsters = await _encounterService.GetMonstersAsync();
            if (monsters.Count == 0)
                return await ReplyWithErrorAsync("No monsters saved yet. Write one up with /dm monster save.");

            // Discord allows 25 fields per embed
            var fields = monsters.Take(25)
                .Select(m => (IEmbedField)new EmbedField(m.Name, EnemySheet.Summary(m)))
                .ToList();

            return await ReplyAsync("Monsters", monsters.Count > 25 ? $"Showing 25 of {monsters.Count}." : string.Empty, fields);
        }

        [Command("show")]
        [Description("Show a saved monster")]
        public async Task<IResult> ShowAsync(
            [Description(MonsterDescription)]
            [AutocompleteProvider(MonsterAutocomplete.ID)]
            string name)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindMonsterAsync(name) is not { } monster)
                return await ReplyWithErrorAsync(NotFound(name));

            return await ReplyAsync(monster.Name, string.Empty, EnemySheet.Fields(monster));
        }

        [Command("remove")]
        [Description("Delete a saved monster. Enemies already made from it stay in their fights")]
        public async Task<IResult> RemoveAsync(
            [Description(MonsterDescription)]
            [AutocompleteProvider(MonsterAutocomplete.ID)]
            string name)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (await _encounterService.FindMonsterAsync(name) is not { } monster)
                return await ReplyWithErrorAsync(NotFound(name));

            await _encounterService.RemoveMonsterAsync(monster);

            return await ReplyAsync("Monster Deleted", $"{monster.Name} is no longer saved.");
        }

        private static string NotFound(string name) => $"There's no saved monster called {name}. See /dm monster list";

        private async Task<Result> ReplyNotGameMasterAsync() =>
            await ReplyWithErrorAsync($"Only someone with the {_gameMaster.RoleName} role can do that.");
    }
}
