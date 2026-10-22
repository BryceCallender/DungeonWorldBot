using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.GameMaster;

public partial class DmCommand
{
    [Group("npc")]
    public class NpcCommands : DungeonWorldCommandBase
    {
        private const string NameDescription = "The NPC";

        private readonly INpcService _npcService;
        private readonly IGameMasterService _gameMaster;

        public NpcCommands(ICommandContext context, FeedbackService feedbackService, INpcService npcService, IGameMasterService gameMaster)
            : base(context, feedbackService)
        {
            _npcService = npcService;
            _gameMaster = gameMaster;
        }

        [Command("add")]
        [Description("Add an NPC to the party: a hireling, guide, companion or anyone else travelling with them")]
        public async Task<IResult> AddAsync(
            [Description("Their name")]
            string name,
            [Description("Their max HP")]
            [Option("max-hp")]
            [MinValue(1)]
            int maxHp,
            [Description("What they are to the party, e.g. Hireling, Guide, Animal companion")]
            string? role = null,
            [Description("Armor against physical damage")]
            [MinValue(0)]
            int armor = 0,
            [Description("Dice they roll for damage, e.g. d6")]
            string? damage = null,
            [Description("A hireling's loyalty")]
            int loyalty = 0,
            [Description("A hireling's skills, e.g. Protector +2, Tracker +1")]
            string? skills = null,
            [Description("What a hireling wants for their service, e.g. Money")]
            string? cost = null,
            [Description("Anything else worth remembering")]
            string? notes = null,
            [Description("The player whose character they follow, like a Ranger's animal companion")]
            IUser? follows = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            name = name.Trim();
            if (name.Length == 0)
                return await ReplyWithErrorAsync("Give the NPC a name.");
            if (await _npcService.FindAsync(name) is { } existing)
                return await ReplyWithErrorAsync($"{existing.Name} is already with the party.");

            var npc = new Npc
            {
                Name = name,
                Role = Text(role),
                CurrentHP = maxHp,
                MaxHP = maxHp,
                Armor = armor,
                Damage = Text(damage),
                Loyalty = loyalty,
                Skills = Text(skills),
                Cost = Text(cost),
                Notes = Text(notes),
                FollowsID = follows?.ID,
            };
            await _npcService.AddAsync(npc);

            return await ReplyAsync("NPC Joined the Party!", NpcSheet.Title(npc), NpcSheet.Fields(npc));
        }

        [Command("edit")]
        [Description("Change an NPC. Only what you fill in changes")]
        public async Task<IResult> EditAsync(
            [Description(NameDescription)]
            [AutocompleteProvider(NpcAutocomplete.ID)]
            string name,
            [Description("A new name")]
            string? rename = null,
            [Description("Their max HP. Their current HP is kept, up to the new max")]
            [Option("max-hp")]
            [MinValue(1)]
            int? maxHp = null,
            [Description("Set their current HP")]
            [MinValue(0)]
            int? hp = null,
            [Description("What they are to the party. " + ClearHint)]
            string? role = null,
            [Description("Armor against physical damage")]
            [MinValue(0)]
            int? armor = null,
            [Description("Dice they roll for damage. " + ClearHint)]
            string? damage = null,
            [Description("Set their loyalty")]
            int? loyalty = null,
            [Description("Add to (or with a minus, take from) their loyalty")]
            [Option("loyalty-change")]
            int? loyaltyChange = null,
            [Description("A hireling's skills. " + ClearHint)]
            string? skills = null,
            [Description("What a hireling wants for their service. " + ClearHint)]
            string? cost = null,
            [Description("Anything else worth remembering. " + ClearHint)]
            string? notes = null,
            [Description("The player whose character they now follow")]
            IUser? follows = null,
            [Description("Stop them following anyone in particular")]
            [Option("follows-nobody")]
            bool followsNobody = false)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            var npc = await _npcService.FindAsync(name);
            if (npc is null)
                return await ReplyWithErrorAsync(NpcCommand.NotFound(name));

            if (!string.IsNullOrWhiteSpace(rename) && !rename.Trim().Equals(npc.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (await _npcService.FindAsync(rename) is { } taken)
                    return await ReplyWithErrorAsync($"{taken.Name} is already with the party.");
            }

            if (!string.IsNullOrWhiteSpace(rename))
                npc.Name = rename.Trim();
            if (maxHp is { } max)
                npc.MaxHP = max;
            if (hp is { } current)
                npc.CurrentHP = current;
            if (armor is { } newArmor)
                npc.Armor = newArmor;
            if (loyalty is { } newLoyalty)
                npc.Loyalty = newLoyalty;
            if (loyaltyChange is { } change)
                npc.Loyalty += change;

            npc.Role = Change(npc.Role, role);
            npc.Damage = Change(npc.Damage, damage);
            npc.Skills = Change(npc.Skills, skills);
            npc.Cost = Change(npc.Cost, cost);
            npc.Notes = Change(npc.Notes, notes);

            if (followsNobody)
                npc.FollowsID = null;
            else if (follows is not null)
                npc.FollowsID = follows.ID;

            await _npcService.SaveAsync(npc);

            return await ReplyAsync("NPC Updated!", NpcSheet.Title(npc), NpcSheet.Fields(npc));
        }

        [Command("stats")]
        [Description("Set an NPC's stat modifiers (-3 to +3). Only what you fill in changes")]
        public async Task<IResult> SetStatsAsync(
            [Description(NameDescription)]
            [AutocompleteProvider(NpcAutocomplete.ID)]
            string name,
            [Description("Strength modifier")] [MinValue(-3)] [MaxValue(3)] int? str = null,
            [Description("Dexterity modifier")] [MinValue(-3)] [MaxValue(3)] int? dex = null,
            [Description("Constitution modifier")] [MinValue(-3)] [MaxValue(3)] int? con = null,
            [Description("Intelligence modifier")] [MinValue(-3)] [MaxValue(3)] int? @int = null,
            [Description("Wisdom modifier")] [MinValue(-3)] [MaxValue(3)] int? wis = null,
            [Description("Charisma modifier")] [MinValue(-3)] [MaxValue(3)] int? cha = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            var npc = await _npcService.FindAsync(name);
            if (npc is null)
                return await ReplyWithErrorAsync(NpcCommand.NotFound(name));

            var changes = new (StatType Stat, int? Value)[]
            {
                (StatType.Str, str), (StatType.Dex, dex), (StatType.Con, con),
                (StatType.Int, @int), (StatType.Wis, wis), (StatType.Cha, cha),
            };
            foreach (var (stat, value) in changes)
            {
                if (value is { } modifier)
                    npc.SetModifier(stat, modifier);
            }

            await _npcService.SaveAsync(npc);

            return await ReplyAsync("NPC Stats Updated!", $"{npc.Name}: {NpcSheet.Stats(npc)}");
        }

        [Command("remove")]
        [Description("Take an NPC off the party for good: they left, or they're dead")]
        public async Task<IResult> RemoveAsync(
            [Description(NameDescription)]
            [AutocompleteProvider(NpcAutocomplete.ID)]
            string name)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            var npc = await _npcService.FindAsync(name);
            if (npc is null)
                return await ReplyWithErrorAsync(NpcCommand.NotFound(name));

            await _npcService.RemoveAsync(npc);

            return await ReplyAsync("NPC Left the Party", $"{npc.Name} is no longer with the party.");
        }

        private async Task<Result> ReplyNotGameMasterAsync() =>
            await ReplyWithErrorAsync($"Only someone with the {_gameMaster.RoleName} role can do that.");
    }
}
