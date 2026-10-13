using System.ComponentModel;
using System.Drawing;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.CharacterCreation;
using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("character")]
public class CharacterCommand : CommandGroup
{
    private const string NoCharacterMessage = "No character exists. Try making one with /character create";

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly CharacterCreationDialogue _creationDialogue;
    private readonly IDiceRoller _diceRoller;
    private readonly IResourceService _resourceService;
    private readonly IGameMasterService _gameMaster;
    private readonly ILevelingService _leveling;

    public CharacterCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        CharacterCreationDialogue creationDialogue,
        IDiceRoller diceRoller,
        IResourceService resourceService,
        IGameMasterService gameMaster,
        ILevelingService leveling)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _creationDialogue = creationDialogue;
        _diceRoller = diceRoller;
        _resourceService = resourceService;
        _gameMaster = gameMaster;
        _leveling = leveling;
    }

    [Command("create")]
    [Description("Create your character for the campaign")]
    public async Task<IResult> CreateCharacterAsync()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

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
        embedFields.Add(new EmbedField(Name: "Debilities", Value: "None"));
        embedFields.Add(new EmbedField(Name: "Total Armor", Value: character.ArmorRating.ToString()));
        embedFields.Add(new EmbedField(Name: "Bonds", Value:
            character.Bonds?.Count > 0 ? string.Join(", ", character.Bonds.Select(b => b.TargetName)) : "None"));
        var abilities = character.Abilities?.Count > 0 ? string.Join(", ", character.Abilities.Select(a => a.ToDisplay())) : "None";
        // Embed field values are capped at 1024 characters
        embedFields.Add(new EmbedField(Name: "Abilities", Value:
            abilities.Length <= 1024 ? abilities : $"{character.Abilities!.Count} abilities. See /ability list"));

        if (_leveling.GetStatPoints(character) is > 0 and var statPoints)
        {
            embedFields.Add(new EmbedField(Name: "Unspent Stat Points", Value: $"{statPoints}. Spend them with /character allocate-stat"));
        }

        var resources = ClassSheetLibrary.For(character.Class.Type)?.ResourcesFor(ClassSheetLibrary.KnownMoves(character)).ToList() ?? [];
        if (resources.Count > 0)
        {
            embedFields.Add(new EmbedField(Name: "Resources", Value: string.Join(", ", resources.Select(r =>
                $"{r.Name} {_resourceService.Format(character, r)}"))));
        }

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: character.Name,
                Thumbnail: ImageHelper.ClassThumbnail(character.Class.Type),
                Description: $"{character.Class.Type}: Level {character.Level}",
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
    [Description("Apply damage to a player's character")]
    public async Task<IResult> DamagePlayerAsync(
        [Description("The user to apply damage to.")]
        IUser user,
        [Description("Amount to damage. Can be a dice roll or just flat damage")]
        string damage)
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
        var amount = Math.Max(0, roll.Value);
        await _characterService.DamageAsync(character, amount);

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Damaged!",
                Description: $"{character.Name} took {amount} damage! ({character.Health.CurrentHP}/{character.Health.MaxHP} HP)",
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

    [Command("stat-change")]
    [Description("DM only: set any character's stat to a value")]
    public async Task<IResult> ChangeStatAsync(
        [Description("The stat to change")]
        StatType type,
        [Description("The new value of the stat")]
        int value,
        [Description("The player whose character to change. Leave blank for your own.")]
        IUser? user = null)
    {
        if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
            return await ReplyNotGameMasterAsync("Players raise stats with /character allocate-stat when they level up.");

        var character = await GetCharacterAsync(user);
        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");
        }

        await _characterService.UpdateCharacterStatAsync(character, type, value);

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Stat Changed!",
                Description: $"{character.Name}'s {type} was changed to {value}",
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("allocate-stat")]
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

    [Command("grant-stat-points")]
    [Description("DM only: give a character stat points to spend")]
    public async Task<IResult> GrantStatPointsAsync(
        [Description("The player whose character gets the points")]
        IUser user,
        [Description("How many points")]
        [MinValue(1)]
        int amount = 1)
    {
        if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
            return await ReplyNotGameMasterAsync();

        var character = await _characterService.GetCharacterFromUserAsync(user.ID);
        if (character is null)
        {
            return await ReplyWithErrorAsync($"{user.Username} does not have a character.");
        }

        await _leveling.GrantStatPointsAsync(character, amount);

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Stat Points Granted!",
                Description: $"{character.Name} now has {_leveling.GetStatPoints(character)} stat point(s) to spend with /character allocate-stat",
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("damage-modifier-change")]
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

    [Command("set-level")]
    [Description("DM only: set a character's level, or everyone's")]
    public async Task<IResult> SetLevelAsync(
        [Description("The new level")]
        [MinValue(1)]
        int level,
        [Description("The player whose character to change. Leave blank for your own.")]
        IUser? user = null,
        [Description("Set every character to this level")]
        bool everyone = false)
    {
        if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
            return await ReplyNotGameMasterAsync("Players can use /character level-up when they level.");

        if (everyone)
        {
            var characters = await _characterService.GetCharactersAsync();
            foreach (var each in characters)
            {
                await _leveling.ChangeLevelAsync(each, level);
            }

            return await _feedbackService.SendContextualEmbedAsync(
                new Embed(
                    Title: "Party Level Changed!",
                    Description: $"Every character is now level {level}: {string.Join(", ", characters.Select(c => c.Name))}\n" +
                                 "Anyone who went up a level has stat points to spend with /character allocate-stat",
                    Colour: _feedbackService.Theme.Primary
                ),
                ct: CancellationToken
            );
        }

        var character = await GetCharacterAsync(user);
        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");
        }

        await _leveling.ChangeLevelAsync(character, level);

        return await ReplyWithLevelChangedAsync(character);
    }

    [Command("level-up")]
    [Description("Increase your character's level by one (the DM can level up someone else)")]
    public async Task<IResult> LevelUpAsync(
        [Description("DM only: the player whose character to level up. Leave blank for your own.")]
        IUser? user = null)
    {
        if (user is not null && _context.TryGetUserID(out var self) && user.ID != self && !await _gameMaster.IsGameMasterAsync(CancellationToken))
            return await ReplyNotGameMasterAsync("You can only level up your own character.");

        var character = await GetCharacterAsync(user);
        if (character is null)
        {
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");
        }

        await _leveling.ChangeLevelAsync(character, character.Level + 1);

        return await ReplyWithLevelChangedAsync(character);
    }

    private async Task<Character?> GetCharacterAsync(IUser? user) =>
        user is null ? await GetOwnCharacterAsync() : await _characterService.GetCharacterFromUserAsync(user.ID);

    private static string FormatModifier(int modifier) => modifier >= 0 ? $"+{modifier}" : modifier.ToString();

    private async Task<Result> ReplyNotGameMasterAsync(string? hint = null)
    {
        var message = $"Only someone with the {_gameMaster.RoleName} role can do that.";
        return await ReplyWithErrorAsync(hint is null ? message : $"{message} {hint}");
    }

    private async Task<Character?> GetOwnCharacterAsync()
    {
        if (!_context.TryGetUserID(out var userId))
        {
            throw new NotSupportedException();
        }

        return await _characterService.GetCharacterFromUserAsync(userId);
    }

    private async Task<IResult> ReplyWithLevelChangedAsync(Character character)
    {
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Level Changed!",
                Description: $"{character.Name} is now level {character.Level}."
                             + (ClassSheetLibrary.For(character.Class.Type) is not null && character.Level >= 2
                                 ? "\nPick a new advanced move with /class moves and /class learn"
                                 : string.Empty)
                             + (_leveling.GetStatPoints(character) is > 0 and var points
                                 ? $"\nRaise a stat with /character allocate-stat ({points} point{(points == 1 ? "" : "s")} to spend)"
                                 : string.Empty),
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    private async Task<Result> ReplyWithErrorAsync(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }
}
