using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using EnumsNET;
using DSharpPlus.CommandsNext;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;
using System.Drawing;
using Remora.Discord.Gateway;
using Remora.Discord.Interactivity.Services;
using DungeonWorldBot.Services.Implementation.Steps;
using DungeonWorldBot.Interactions;
using CommandGroup = Remora.Commands.Groups.CommandGroup;
using DungeonWorldBot.Services.Interactivity;

namespace DungeonWorldBot.Commands;

[Group("character")]
public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly InteractivityService _interactivityService;
    private readonly IDiscordRestUserAPI _userAPI;
    private readonly IDiscordRestGuildAPI _guildAPI;

    public CharacterCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiscordRestChannelAPI channelAPI,
        InteractivityService interactivityService, 
        IDiscordRestUserAPI userAPI,
        IDiscordRestGuildAPI guildAPI)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _guildAPI = guildAPI;
        _channelAPI = channelAPI;
        _interactivityService = interactivityService;
        _userAPI = userAPI;
    }

    [Command("create")]
    public async Task<IResult> CreateCharacterAsync()
    {
        
        var available = await _characterService.GetCharacterFromUserAsync(_context.User);
        if (available is not null)
        {
            return await ReplyWithCharacterFailureAsync();
        }

        var userChannel = _context.User.ID;

        await _feedbackService.SendPrivateMessageAsync(userChannel, new FeedbackMessage("No character exists, starting character creation.", Color.Green));

        var character = new Character
        {
            ID = userChannel,
            Level = 1,
            ArmorRating = 0,
            Status = Status.Alive,
        };

        var name = string.Empty;
        var race = new Race();
        var chosenClass = new Class();

        var statValueList = new List<int>();

        var strStat = new Stat();
        var dexStat = new Stat();
        var conStat = new Stat();
        var intStat = new Stat();
        var wisStat = new Stat();
        var chaStat = new Stat();
        var statList = new List<Stat>();

        var alignment = new Alignment();

        statValueList.Add(8);
        statValueList.Add(9);
        statValueList.Add(12);
        statValueList.Add(13);
        statValueList.Add(15);
        statValueList.Add(16);

        var editStep = new EndingStep("1. Edit the Name \n2. Edit the Race \n3. Edit the Class \n" +
            "4. Edit the Stats \n5. Edit the Alignment \n6. No Editing \n Please select one of these by entering a number.", _feedbackService, null, character, 1, 6);
        var alignStep = new IntStep("What is your characters Alignment?\n" +
            "Options inclue: \n1. Lawful Good \n2. Neutral Good \n3. Chaotic Good \n4. Lawful Neutral \n5. True Neutral \n" +
            "6. Chaotic Neutral \n7. Lawful Evil \n8. Neutral Evil \n9. Chaotic Evil \n10. Unaligned \n" +
            "Please select one of these by entering a number.", _feedbackService, editStep, 1, 10);
        var chaStep = new StatStep("Which value would you like your Charisma stat to be?", _feedbackService, alignStep, statValueList);
        var wisStep = new StatStep("Which value would you like your Wisdom stat to be?", _feedbackService, chaStep, statValueList);
        var intStep = new StatStep("Which value would you like your Intelligence stat to be?", _feedbackService, wisStep, statValueList);
        var conStep = new StatStep("Which value would you like your Constitution stat to be?", _feedbackService, intStep, statValueList);
        var dexStep = new StatStep("Which value would you like your Dexterity stat to be?", _feedbackService, conStep, statValueList);
        var strStep = new StatStep("Which value would you like your Strength stat to be?", _feedbackService, dexStep, statValueList);
        var classStep = new IntStep("What is your characters Class?\n" +
            "Current Options: \n1. Channeler \n2. Necromancer \n3. Paladin \n" +
            "4. Ranger \n5. Slayer \nPlease select one of these by entering a number.", _feedbackService, strStep, 1, 5);
        var raceStep = new IntStep("What is your characters Race?\n" +
            "Current Options: \n1. Elf \n2. Dwarf \n3. Human \n" +
            "4. Orc \nPlease select one of these by entering a number.", _feedbackService, classStep, 1, 4);
        var nameStep = new TextStep("What is your characters Name?", _feedbackService, raceStep, 2, 100);

        nameStep.OnValidResult += (result) =>
        {
            name = result;
            character.Name = name;  
        };
        raceStep.OnValidResult += (result) =>
        {
            race = (Race)result-1;
            character.Race = race;
        };
        classStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    chosenClass.Type = ClassType.Channeler;
                    break;
                case 2:
                    chosenClass.Type = ClassType.Necromancer;
                    break;
                case 3:
                    chosenClass.Type = ClassType.Paladin;
                    break;
                case 4:
                    chosenClass.Type = ClassType.Ranger;
                    break;
                case 5:
                    chosenClass.Type = ClassType.Slayer;
                    break;

            };

            character.Class = chosenClass;
            chosenClass.Character = character;
        };

        strStep.OnValidResult += (result) =>
        {
            strStat.StatType = StatType.Str;
            strStat.Value = result;
            statList.Add(strStat);
        };
        dexStep.OnValidResult += (result) =>
        {
            dexStat.StatType = StatType.Dex;
            dexStat.Value = result;
            statList.Add(dexStat);
        };
        conStep.OnValidResult += (result) =>
        {
            conStat.StatType = StatType.Con;
            conStat.Value = result;
            statList.Add(conStat);
        };
        intStep.OnValidResult += (result) =>
        {
            intStat.StatType = StatType.Int;
            intStat.Value = result;
            statList.Add(intStat);
        };
        wisStep.OnValidResult += (result) =>
        {
            wisStat.StatType = StatType.Wis;
            wisStat.Value = result;
            statList.Add(wisStat);
        };
        chaStep.OnValidResult += (result) =>
        {
            chaStat.StatType = StatType.Cha;
            chaStat.Value = result;
            statList.Add(chaStat);
            character.Stats = statList;
        };
        alignStep.OnValidResult += (result) => 
        {
            alignment = (Alignment)result;
            character.Alignment = alignment;
        };

        editStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    editStep.setNextStep(nameStep);
                    nameStep.setNextStep(editStep);
                    break;
                case 2:
                    editStep.setNextStep(raceStep);
                    raceStep.setNextStep(editStep);
                    break;
                case 3:
                    editStep.setNextStep(classStep);
                    classStep.setNextStep(editStep);
                    break;
                case 4:
                    editStep.setNextStep(strStep);
                    chaStep.setNextStep(editStep);
                    statValueList.Add(8);
                    statValueList.Add(9);
                    statValueList.Add(12);
                    statValueList.Add(13);
                    statValueList.Add(15);
                    statValueList.Add(16);
                    statList.RemoveAll(s => s == s);
                    break;
                case 5:
                    editStep.setNextStep(alignStep);
                    alignStep.setNextStep(editStep);
                    break;
                case 6:
                    editStep.setNextStep(null);
                    break;

            };
        };

        

        var inputDialogueHandler = new DialogueHandler(_interactivityService, _context.User, nameStep, _userAPI);

        var succeeded = await inputDialogueHandler.ProcessDialogue(_feedbackService).ConfigureAwait(false);

        if (!succeeded) { return Result.FromSuccess(); }

        character.Health = new Health
        {
            MaxHP = conStat.Value + chosenClass.HealthModifier,
            CurrentHP = conStat.Value + chosenClass.HealthModifier,
        };
        
        var embedFields = new List<IEmbedField>();

        embedFields.Add(new EmbedField(Name: "Race", Value: character.Race.ToString(), IsInline: false));
        embedFields.AddRange(character.Stats.Select(s => new EmbedField(Name: s.StatType.ToString(), Value: s.Value.ToString(), true)).ToArray());

        embedFields.Add(new EmbedField(Name: "Health", Value: character.Health.ToDisplay(), IsInline: false));
        embedFields.Add(new EmbedField(Name: "Status", Value: character.Status?.ToString() ?? "Unknown..."));
        embedFields.Add(new EmbedField(Name: "Alignment", Value: character.Alignment.ToString()));
        embedFields.Add(new EmbedField(Name: "Debilities", Value: "None"));

        var embed = new Embed 
        {
                Title = $"{character.Name}",
                Type = EmbedType.Rich,
                Description = $"{character.Class.Type}: Level 1",
                Fields = embedFields,
                Colour = _feedbackService.Theme.Primary
        };

        await _feedbackService.SendPrivateEmbedAsync(userChannel, embed);

        await _characterService.AddCharacterAsync(character);

        var embedEnd = new Embed
        {
            Title = "Character Creation",
            Description = "Your Character has been successfully saved. Try typing /profile to see your character sheet.",
            Colour = Color.Green
        };

        await _feedbackService.SendPrivateEmbedAsync(userChannel, embedEnd);

        return Result.FromSuccess();
    }


    [Command("profile")]
    public async Task<IResult> ShowCharacterSheetAsync()
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);
        if (character is null)
        {
            return await ReplyWithFailureAsync();
        }

        var embedFields = new List<IEmbedField>();
        
        embedFields.AddRange(character.Stats.Select(s => new EmbedField(Name: s.StatType.ToString(), Value: s.Value.ToString(), true)).ToArray());

        embedFields.Add(new EmbedField(Name: "Health", Value: character.Health.ToDisplay(), IsInline: false));
        embedFields.Add(new EmbedField(Name: "Status", Value: character.Status?.ToString() ?? "Unknown..."));
        embedFields.Add(new EmbedField(Name: "Alignment", Value: character.Alignment.ToString()));
        embedFields.Add(new EmbedField(Name: "Debilities", Value: "None"));
        embedFields.Add(new EmbedField(Name: "Bonds", Value: "None"));

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}",
                Type: EmbedType.Rich,
                Description: $"{character.Class.Type.ToString()}: Level {character.Level}",
                Fields: embedFields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }
    
    [Command("alignment")]
    [SuppressInteractionResponse(true)]
    public async Task<IResult> ChangeAlignment(
        [Description("The user to change the alignment of. Leave empty if changing you're own")]
        IUser? user = null)
    {
        if (user is not null)
        {
            var rolesResult = await _guildAPI.GetGuildRolesAsync(_context.GuildID.Value);
            
            if (!rolesResult.IsDefined(out var guildRoles))
                return Result<(IRole, IRole[])>.FromError(rolesResult.Error!);
            
            var memberResult = await _guildAPI.GetGuildMemberAsync(_context.GuildID.Value, _context.User.ID);

            if (!memberResult.IsDefined(out var member))
                return Result<(IRole, IRole[])>.FromError(memberResult.Error!);

            var dmRole = guildRoles.SingleOrDefault(r => r.Name.Equals("DM"));

            if (dmRole == null || !member.Roles.Contains(dmRole.ID))
            {
                return Result.FromError<string>("Only a DM can change another players alignment");
            }
        }
        
        user ??= _context.User;

        var character = await _characterService.GetCharacterFromUserAsync(user);
        if (character is null)
        {
            return Result.FromError<string>("User does not exist or have a current character");
        }
        
        var embed = new Embed(Description: "Select a alignment below.");
        var options = new FeedbackMessageOptions(MessageComponents: new IMessageComponent[]
        {
            new ActionRowComponent(new[]
            {
                new SelectMenuComponent
                (
                    CustomIDHelpers.CreateSelectMenuID("alignment-dropdown"),
                    Enums.GetValues<Alignment>()
                        .Where(x => x != Alignment.Unknown)
                        .Select(x => new SelectOption(Label: x.ToString(), Value: x.ToString()))
                        .ToArray(),
                    "Alignment...",
                    1,
                    1
                )
            })
        });

        return await _feedbackService.SendContextualEmbedAsync(embed, options, this.CancellationToken);
    }

    private async Task<Result> ReplyWithFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "No character exists. Try making one with /character create",
            ct: CancellationToken
        );
    }

    private async Task<Result> ReplyWithCharacterFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "You already have a character. Check your character sheet using /character profile",
            ct: CancellationToken
        );
    }

    private async Task<Result> ReplyWithError(string error)
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }
}