using DungeonWorldBot.Services;
using DSharpPlus.CommandsNext;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using System.Drawing;
using Remora.Discord.Gateway;
using Remora.Discord.API.Objects;
using Remora.Discord.Interactivity.Services;
using Remora.Discord.Interactivity;
using DungeonWorldBot.Services.Implementation.Steps;
using DungeonWorldBot.Interactions;
using CommandGroup = Remora.Commands.Groups.CommandGroup;
using Remora.Discord.API.Abstractions.Rest;
using DungeonWorldBot.Services.Interactivity;
using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;

namespace DungeonWorldBot.Commands;

public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly InteractivityService _interactivityService;
    private readonly IDiscordRestUserAPI _userAPI;

    public CharacterCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiscordRestChannelAPI channelAPI,
        InteractivityService interactivityService, 
        IDiscordRestUserAPI userAPI)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _channelAPI = channelAPI;
        _interactivityService = interactivityService;
        _userAPI = userAPI;
    }

    [Command("create")]
    public async Task<IResult> CreateCharacterAsync()
    {
        var character = new Character();
        var name = string.Empty;
        var race = string.Empty;
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

        //var endingStep = new EndingStep
        var alignStep = new IntStep("What alignment is your character?\n" +
            "Options inclue: \n1. Lawful Good \n2. Neutral Good \n 3. Chaotic Good \n 4. Lawful Neutral \n 5. True Neutral \n" +
            "6. Chaotic Neutral \n7. Lawful Evil \n 8. Neutral Evil \n 9. Chaotic Evil \n" +
            "Please select one of these by entering a number.", _feedbackService, null);
        var chaStep = new StatStep("Which value would you like your Charisma stat to be?", _feedbackService, alignStep, statValueList);
        var wisStep = new StatStep("Which value would you like your Wisdom stat to be?", _feedbackService, chaStep, statValueList);
        var intStep = new StatStep("Which value would you like your Intelligence stat to be?", _feedbackService, wisStep, statValueList);
        var conStep = new StatStep("Which value would you like your Constitution stat to be?", _feedbackService, intStep, statValueList);
        var dexStep = new StatStep("Which value would you like your Dexterity stat to be?", _feedbackService, conStep, statValueList);
        var strStep = new StatStep("Which value would you like your Strength stat to be?", _feedbackService, dexStep, statValueList);
        var classStep = new IntStep("What is your characters Class?\n" +
            "Current Options: \n1. Channeler \n2. Necromancer \n3. Paladin \n" +
            "4. Ranger \n5. Slayer \nPlease select one of these by entering a number.", _feedbackService, strStep);
        var raceStep = new TextStep("What is your characters Race?", _feedbackService, classStep, 2, 100);
        var nameStep = new TextStep("What is your characters Name?", _feedbackService, raceStep, 2, 100);

        nameStep.OnValidResult += (result) =>
        {
            name = result;
            character.Name = name;  
        };
        raceStep.OnValidResult += (result) =>
        {
            race = result;
            //character.Race = race;
        };
        classStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    chosenClass.ClassType = ClassType.Channeler;
                    break;
                case 2:
                    chosenClass.ClassType = ClassType.Necromancer;
                    break;
                case 3:
                    chosenClass.ClassType = ClassType.Paladin;
                    break;
                case 4:
                    chosenClass.ClassType = ClassType.Ranger;
                    break;
                case 5:
                    chosenClass.ClassType = ClassType.Slayer;
                    break;

            };


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
        };
        alignStep.OnValidResult += (result) => alignment = (Alignment)result-1;

        var userChannel = _context.User.ID;

        var inputDialogueHandler = new DialogueHandler(_interactivityService, _context.User, nameStep, _userAPI);

        var succeeded = await inputDialogueHandler.ProcessDialogue(_feedbackService).ConfigureAwait(false);

        if (!succeeded) { return Result.FromSuccess(); }

        var embedFields = new List<IEmbedField>();

        embedFields.AddRange(statList.Select(s => new EmbedField(Name: s.StatType.ToString(), Value: s.Value.ToString(), true)).ToArray());

        //embedFields.Add(new EmbedField(Name: "Health", Value: character.Health.ToDisplay(), IsInline: false));
        //embedFields.Add(new EmbedField(Name: "Status", Value: character.Status?.ToString() ?? "Unknown..."));
        embedFields.Add(new EmbedField(Name: "Alignment", Value: alignment.ToString()));
        //embedFields.Add(new EmbedField(Name: "Debilities", Value: "None"));

        var embed = new Embed 
        {
                Title = $"{name}",
                Type = EmbedType.Rich,
                Description = $"{race} {chosenClass.ClassType.ToString()}: Level 1",
                Fields = embedFields,
                Colour = _feedbackService.Theme.Primary
        };

        await _feedbackService.SendPrivateEmbedAsync(userChannel, embed);

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
        
        return Result.FromSuccess();
    }

    private async Task<Result> ReplyWithFailureAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync
        (
            "No character exists. Try making one with /create",
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