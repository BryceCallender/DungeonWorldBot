using System.ComponentModel;
using System.Drawing;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Characters;

[Group("character")]
public class CharacterCommand : CharacterCommandBase
{
    private readonly CharacterCreationDialogue _creationDialogue;
    private readonly IDiceRoller _diceRoller;
    private readonly IResourceService _resourceService;
    private readonly IArmorService _armorService;

    public CharacterCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        CharacterCreationDialogue creationDialogue,
        IDiceRoller diceRoller,
        IResourceService resourceService,
        IGameMasterService gameMaster,
        ILevelingService leveling,
        IGameSettingsService settings,
        IArmorService armorService)
        : base(context, feedbackService, characterService, gameMaster, leveling, settings)
    {
        _creationDialogue = creationDialogue;
        _diceRoller = diceRoller;
        _resourceService = resourceService;
        _armorService = armorService;
    }

    [Command("create")]
    [Description("Create your character for the campaign")]
    public async Task<IResult> CreateCharacterAsync()
    {
        var userId = GetUserId();

        if (await _characterService.GetCharacterFromUserAsync(userId) is not null)
        {
            return await ReplyWithErrorAsync("You already have a character. Check your character sheet using /character profile");
        }

        await _feedbackService.SendContextualNeutralAsync("Check your DMs to create your character!", ct: CancellationToken);
        await _feedbackService.SendPrivateMessageAsync(userId, new FeedbackMessage("No character exists, starting character creation.", Color.Green), ct: CancellationToken);

        var character = await _creationDialogue.RunAsync(userId, CancellationToken);
        if (character is null)
        {
            await _feedbackService.SendPrivateMessageAsync(userId, new FeedbackMessage("Character creation was cancelled.", Color.Orange), ct: CancellationToken);
            return Result.FromSuccess();
        }

        await _characterService.AddCharacterAsync(character);

        await _feedbackService.SendPrivateEmbedAsync(userId, new Embed
        {
            Title = character.Name,
            Thumbnail = ImageHelper.ClassThumbnail(character.Class.Type),
            Description = $"{character.Class.Type}: Level 1",
            Fields = CharacterSheet.NewCharacterFields(character),
            Colour = _feedbackService.Theme.Primary
        }, ct: CancellationToken);

        await _feedbackService.SendPrivateEmbedAsync(userId, new Embed
        {
            Title = "Character Creation",
            Description = "Your Character has been successfully saved. Try typing /character profile to see your character sheet.",
            Colour = Color.Green
        }, ct: CancellationToken);

        return Result.FromSuccess();
    }

    [Command("profile")]
    [Description("Show a character sheet")]
    public async Task<IResult> ShowCharacterSheetAsync(
        [Description("The user to get profile of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await _characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null
                ? NoCharacterMessage
                : $"{user.Username} does not have a character.");
        }

        var embedFields = new List<IEmbedField>();

        embedFields.AddRange(character.Stats.Select(s => new EmbedField(Name: s.StatType.ToString(), Value: $"{s.Value} ({(s.Modifier >= 0 ? "+" : string.Empty)}{s.Modifier})", true)));

        embedFields.Add(new EmbedField(Name: "Health", Value: character.Health.ToDisplay(), IsInline: false));
        embedFields.Add(new EmbedField(Name: "Status", Value: character.Status?.ToString() ?? "Unknown..."));
        embedFields.Add(new EmbedField(Name: "Alignment", Value: character.Alignment.ToString()));
        embedFields.Add(new EmbedField(Name: "Debilities", Value: DebilityRules.Current(character) is { Count: > 0 } debilities
            ? string.Join(", ", debilities.Select(DebilityRules.Describe))
            : "None"));
        embedFields.Add(new EmbedField(Name: "Armor", Value: DamageRules.Summary(character)));
        embedFields.Add(new EmbedField(Name: "Bonds", Value:
            character.Bonds?.Count > 0 ? string.Join(", ", character.Bonds.Select(b => b.TargetName)) : "None"));
        var abilities = character.Abilities?.Count > 0 ? string.Join(", ", character.Abilities.Select(a => a.ToDisplay())) : "None";
        // Embed field values are capped at 1024 characters
        embedFields.Add(new EmbedField(Name: "Abilities", Value:
            abilities.Length <= 1024 ? abilities : $"{character.Abilities!.Count} abilities. See /ability list"));

        if (_leveling.GetStatPoints(character) is > 0 and var statPoints)
        {
            embedFields.Add(new EmbedField(Name: "Unspent Stat Points", Value: $"{statPoints}. Spend them with /character level allocate"));
        }

        var resources = ClassSheetLibrary.For(character.Class.Type)?.ResourcesFor(ClassSheetLibrary.KnownMoves(character)).ToList() ?? [];
        if (resources.Count > 0)
        {
            embedFields.Add(new EmbedField(Name: "Resources", Value: string.Join(", ", resources.Select(r =>
                $"{r.Name} {_resourceService.Format(character, r)}"))));
        }

        var gear = GearRules.Owned(character);
        if (gear.Count > 0)
        {
            embedFields.Add(new EmbedField(Name: "Gear", Value: string.Join('\n', gear.Select(g =>
                $"{g.Gear.Name}: {g.Mode?.Name ?? $"no {g.Gear.ModeKind.ToLower()}"}" +
                (g.Mode?.Counter is { } counter ? $" ({counter.Name} {_resourceService.Format(character, counter.Resource)})" : string.Empty)))));
        }

        if (ShapeshiftRules.CanShapeshift(character))
        {
            embedFields.Add(new EmbedField(Name: "Form", Value: ShapeshiftRules.Status(character, _resourceService.Format(character, ShapeshiftRules.Hold))));
        }

        var subclass = SubclassLibrary.Of(character);
        if (subclass?.Status?.Invoke(character) is { } subclassStatus)
        {
            embedFields.Add(new EmbedField(Name: subclass.Name, Value: subclassStatus));
        }

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: character.Name,
                Thumbnail: ImageHelper.ClassThumbnail(character.Class.Type),
                Description: $"{character.Class.Type}{(subclass is not null ? $" · {subclass.Name}" : string.Empty)}: Level {character.Level}",
                Fields: embedFields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("bond")]
    [Description("Form a bond with another player's character")]
    public async Task<IResult> AddCharacterBondAsync(
        [Description("The user to bond with")]
        IUser user)
    {
        var character = await GetOwnCharacterAsync();
        var bondTo = await _characterService.GetCharacterFromUserAsync(user.ID);

        if (character is null || bondTo is null)
        {
            return await ReplyWithErrorAsync("Either you do not have a character or the requested user does not have a character.");
        }

        await _characterService.BondWithAsync(character, bondTo);

        return await _feedbackService.SendContextualNeutralAsync(
            $"You are now bonded with {bondTo.Name}",
            ct: CancellationToken
        );
    }

    [Command("damage")]
    [Description("Apply damage to a player's character, reduced by their armor against its type")]
    public async Task<IResult> DamagePlayerAsync(
        [Description("The user to apply damage to.")]
        IUser user,
        [Description("Amount to damage, before armor. Can be a dice roll or just flat damage")]
        string damage,
        [Description("Physical, Magical or Divine (divine weapons), each stopped by its own armor, or True (ignores armor)")]
        DamageType type = DamageType.Physical,
        [Description("How much of their armor the hit ignores")]
        [MinValue(0)]
        int piercing = 0)
    {
        var character = await _characterService.GetCharacterFromUserAsync(user.ID);
        if (character is null)
        {
            return await ReplyWithErrorAsync($"{user.Username} does not have a character.");
        }

        if (!_diceRoller.TryRoll(damage, out var roll))
        {
            return await ReplyWithErrorAsync($"`{damage}` is not a valid amount or dice roll.");
        }

        // Modifiers can push a roll below zero ("1d4-3"), which shouldn't heal anyone
        var hit = await _armorService.TakeDamageAsync(character, roll.Value, type, piercing);

        var description = $"{character.Name} took {hit.Taken} damage! ({character.Health.CurrentHP}/{character.Health.MaxHP} HP)" +
                          (character.Health.CurrentHP == 0 ? " · at 0 HP, roll /move last-breath" : string.Empty) +
                          $"\n{DamageRules.Describe(hit)}";
        if (hit.Broken.Count > 0)
            description += $"\n*Broken: {string.Join(", ", hit.Broken.Select(b => b.Source))}*";

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Damaged!",
                Description: description,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("heal")]
    [Description("Heal a player's character")]
    public async Task<IResult> HealPlayerAsync(
        [Description("The user to apply healing to.")]
        IUser user,
        [Description("Amount to heal. Can be a dice roll or left blank to full heal")]
        string? heal = null)
    {
        var character = await _characterService.GetCharacterFromUserAsync(user.ID);
        if (character is null)
        {
            return await ReplyWithErrorAsync($"{user.Username} does not have a character.");
        }

        string description;
        if (string.IsNullOrWhiteSpace(heal))
        {
            await _characterService.FullHealAsync(character);
            description = $"{character.Name} was fully healed";
        }
        else
        {
            if (!_diceRoller.TryRoll(heal, out var roll))
            {
                return await ReplyWithErrorAsync($"`{heal}` is not a valid amount or dice roll.");
            }

            var amount = Math.Max(0, roll.Value);
            await _characterService.HealAsync(character, amount);
            description = $"{character.Name} was healed for {amount} HP";
        }

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Healed!",
                Description: $"{description} ({character.Health.CurrentHP}/{character.Health.MaxHP} HP)",
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("damage-dice")]
    [Description("Change the dice your character rolls for damage")]
    public async Task<IResult> ChangeDamageModifierAsync(
        [Description("Dice notation for your damage, e.g. d8+1")]
        string newModifier)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
        {
            return await ReplyWithErrorAsync(NoCharacterMessage);
        }

        if (!_diceRoller.TryRoll(newModifier, out _))
        {
            return await ReplyWithErrorAsync($"`{newModifier}` is not a valid dice roll.");
        }

        await _characterService.UpdateCharacterDamageModifierAsync(character, newModifier);

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Damage Modifier Changed!",
                Description: $"{character.Name}'s Damage modifier was changed to {newModifier}",
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    /// <summary>
    /// Spending the stat points the DM gives out when the party levels up. Levels and points per level are set under /dm level.
    /// </summary>
    [Group("level")]
    public class LevelCommand : CharacterCommandBase
    {
        public LevelCommand(
            ICommandContext context,
            FeedbackService feedbackService,
            ICharacterService characterService,
            IGameMasterService gameMaster,
            ILevelingService leveling,
            IGameSettingsService settings)
            : base(context, feedbackService, characterService, gameMaster, leveling, settings)
        {
        }

        [Command("allocate")]
        [Description("Spend a stat point from levelling up to raise a stat by 1 (max 18)")]
        public async Task<IResult> AllocateStatAsync(
            [Description("The stat to raise")]
            StatType stat)
        {
            var character = await GetOwnCharacterAsync();
            if (character is null)
            {
                return await ReplyWithErrorAsync(NoCharacterMessage);
            }

            if (await _leveling.AllocateStatAsync(character, stat) is { } problem)
            {
                return await ReplyWithErrorAsync(problem);
            }

            var raised = character.Stats.First(s => s.StatType == stat);
            var description = $"{character.Name}'s {stat} is now {raised.Value} ({FormatModifier(raised.Modifier)})";
            if (stat == StatType.Con)
                description += $"\nMax HP is now {character.Health.MaxHP}.";
            if (_leveling.GetStatPoints(character) is > 0 and var left)
                description += $"\n{left} stat point{(left == 1 ? "" : "s")} left.";

            return await _feedbackService.SendContextualEmbedAsync(
                new Embed(
                    Title: "Stat Raised!",
                    Description: description,
                    Colour: _feedbackService.Theme.Primary
                ),
                ct: CancellationToken
            );
        }

        [Command("points")]
        [Description("Show how many stat points each level gives")]
        public async Task<IResult> ShowLevelPointsAsync()
        {
            return await ReplyWithLevelPointsAsync("Stat Points per Level");
        }
    }
}
