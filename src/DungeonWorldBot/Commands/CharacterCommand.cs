using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using EnumsNET;
using Remora.Commands.Attributes;
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
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Services.Implementation.Steps;
using DungeonWorldBot.Interactions;
using DungeonWorldBot.Services.Interactivity;
using Remora.Commands.Groups;
using DungeonWorldBot.Services.Implementation;
using System;

namespace DungeonWorldBot.Commands;

[Group("character")]
public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly InteractivityService _interactivityService;
    private readonly IDiscordRestUserAPI _userAPI;
    private readonly IDiscordRestGuildAPI _guildAPI;
    private readonly Random _random;

    public CharacterCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        InteractivityService interactivityService, 
        IDiscordRestUserAPI userAPI,
        IDiscordRestGuildAPI guildAPI,
        Random random)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _guildAPI = guildAPI;
        _interactivityService = interactivityService;
        _userAPI = userAPI;
        _random = random;
    }

    [Command("create")]
    [Description("Create your character for the campaign")]
    public async Task<IResult> CreateCharacterAsync()
    {
        /*var available = await _characterService.GetCharacterFromUserAsync(_context.User);
        if (available is not null)
        {
            return await ReplyWithCharacterFailureAsync();
        }*/

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

        var inventory = new Inventory();
        var items = new List<Item>();
        var item1 = new Item();
        var item2 = new Item();
        var item3 = new Item();
        var item4 = new Item();
        var item5 = new Item();
        var item6 = new Item();
        var item7 = new Item();


        statValueList.Add(8);
        statValueList.Add(9);
        statValueList.Add(12);
        statValueList.Add(13);
        statValueList.Add(15);
        statValueList.Add(16);

        var editStep = new EndingStep("1. Edit the Name \n2. Edit the Race \n3. Edit the Class \n" +
            "4. Edit the Stats \n5. Edit the Alignment \n6. Edit the Starting Items \n7. No Editing \nPlease select one of these by entering a number.", _feedbackService, null, character, 1, 7);
        

        var channelerThirdItemStep = new IntStep("Which set of items would you like to have? \n" +
            "1. Smelling Salts and 1 Antitoxin \n2. Pouch with 1d10 coins\n" +
            "Please select one of these by entering a number.", _feedbackService, editStep, 1, 2);
        var channelerSecondItemStep = new IntStep("Which of these backup weapons would you like to have? \n" +
            "1. Knife (hand) \n2. Bo Staff (close, Two-Handed) \n" +
            "Please select one of these by entering a number.", _feedbackService, channelerThirdItemStep, 1, 2);
        var channelerFirstItemStep = new IntStep("Which set of items would you like to have? \n" +
            "1. Leather Armor (+1 armor) \n2. 3 Healing Potions and Adventuring Gear (5 uses)\n" +
            "Please select one of these by entering a number.", _feedbackService, channelerSecondItemStep, 1, 2);



        var necroThirdItemStep = new IntStep("Which corpse is in one of your three Hexed Canopic Jars? \n" +
            "1. A One Armed Dwarf \n2. Someone You Used to Know \n3. A Skinless Hound\n" +
            "Please select one of these by entering a number.", _feedbackService, editStep, 1, 3);
        var necroSecondItemStep = new IntStep("Which of these weapons would you like to have? \n" +
            "1. Bone Dagger (hand) \n2. Skull Staff (close, Two-Handed) \n" +
            "Please select one of these by entering a number.", _feedbackService, necroThirdItemStep, 1, 2);
        var necroFirstItemStep = new IntStep("Which set of items would you like to have? \n" +
            "1. Leather Armor (+1 armor) \n2. 3 Healing Potions and Adventuring Gear (5 uses)\n" +
            "Please select one of these by entering a number.", _feedbackService, necroSecondItemStep, 1, 2);


        var paladinSecondItemStep = new IntStep("Which set of items would you like to have? \n" +
            "1. Adventuring Gear (5 uses) \n2. 5 Dungeon Rations and a Healing Potion \n" +
            "Please select one of these by entering a number.", _feedbackService, editStep, 1, 2);
        var paladinFirstItemStep = new IntStep("Which of these weapons would you like to have? \n" +
            "1. Halberd (reach, +1 damage, Two-Handed) \n2. Long Sword (close, +1 damage) and Shield (+1 armor) \n" +
            "Please select one of these by entering a number.", _feedbackService, paladinSecondItemStep, 1, 2);


        var rangerSecondItemStep = new IntStep("Which set of items would you like to have? \n" +
            "1. Adventuring Gear (5 uses) and a Bundle of 3 Arrows \n2. Adventuring Gear (5 uses) and 5 Dungeon Rations \n" +
            "Please select one of these by entering a number.", _feedbackService, editStep, 1, 2);
        var rangerFirstItemStep = new IntStep("Which of these weapons would you like to have? \n" +
            "1. Hunter's Bow (near, far) and Short Sword (close) \n2. Hunter's Bow (near, far) and Spear (reach) \n" +
            "Please select one of these by entering a number.", _feedbackService, rangerSecondItemStep, 1, 2);


        var slayerSecondItemStep = new IntStep("Which set of items would you like to have? \n" +
            "1. 3 Javelins (thrown, near) \n2. Crossbow (near, +1 damage, reload) \n3. A Trophy from a recent kill worth 4d8 coins\n" +
            "Please select one of these by entering a number.", _feedbackService, editStep, 1, 3);
        var slayerFirstItemStep = new IntStep("Which of these weapons would you like to have? \n" +
            "1. Dual Battle Axes (close, +1 damage) \n2. Spear (close, thrown, near) \n3. Claymore (close, +1 damage, Two-Handed) \n" +
            "Please select one of these by entering a number.", _feedbackService, slayerSecondItemStep, 1, 3);


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
            race = (Race)result;
            character.Race = race;
        };
        classStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    chosenClass.Type = ClassType.Channeler;
                    ItemService.setItem(ref item1, "Dungeon Rations", 5, 1);
                    items.Add(item1);
                    ItemService.setItem(ref item2, "Soothing Balm", 1, 0);
                    items.Add(item2);
                    if(classStep._edited)
                    {
                        classStep.setNextStep(channelerFirstItemStep);
                    }
                    alignStep.setNextStep(channelerFirstItemStep);
                    break;
                case 2:
                    chosenClass.Type = ClassType.Necromancer;
                    ItemService.setItem(ref item1, "Dungeon Rations", 5, 1);
                    items.Add(item1);
                    ItemService.setItem(ref item2, "Shovel", 1, 1);
                    items.Add(item2);
                    ItemService.setItem(ref item3, "Needle and Thread", 1, 0);
                    items.Add(item3);
                    if (classStep._edited)
                    {
                        classStep.setNextStep(necroFirstItemStep);
                    }
                    alignStep.setNextStep(necroFirstItemStep);
                    break;
                case 3:
                    chosenClass.Type = ClassType.Paladin;
                    ItemService.setItem(ref item1, "Dungeon Rations", 5, 1);
                    items.Add(item1);
                    ItemService.setItem(ref item2, "Scale Armor", 1, 1);
                    character.ArmorRating = 2;
                    items.Add(item2);
                    ItemService.setItem(ref item3, "Mark of Faith", 1, 0);
                    items.Add(item3);
                    if (classStep._edited)
                    {
                        classStep.setNextStep(paladinFirstItemStep);
                    }
                    alignStep.setNextStep(paladinFirstItemStep);
                    break;
                case 4:
                    chosenClass.Type = ClassType.Ranger;
                    ItemService.setItem(ref item1, "Dungeon Rations", 5, 1);
                    items.Add(item1);
                    ItemService.setItem(ref item2, "Leather Armor", 1, 1);
                    character.ArmorRating = 1;
                    items.Add(item2);
                    ItemService.setItem(ref item3, "Bundle of Arrows", 3, 1);
                    items.Add(item3);
                    if (classStep._edited)
                    {
                        classStep.setNextStep(rangerFirstItemStep);
                    }
                    alignStep.setNextStep(rangerFirstItemStep);
                    break;
                case 5:
                    chosenClass.Type = ClassType.Slayer;
                    ItemService.setItem(ref item1, "Dungeon Rations", 5, 1);
                    items.Add(item1);
                    ItemService.setItem(ref item2, "Leather Armor", 1, 1);
                    character.ArmorRating = 1;
                    items.Add(item2);
                    ItemService.setItem(ref item3, "Slayer's Arsenal", 1, 1);
                    items.Add(item3);
                    if (classStep._edited)
                    {
                        classStep.setNextStep(channelerFirstItemStep);
                    }
                    alignStep.setNextStep(slayerFirstItemStep);
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

        channelerFirstItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item3, "Leather Armor", 1, 1);
                    items.Add(item3);
                    character.ArmorRating = 1;
                    break;
                case 2:
                    ItemService.setItem(ref item3, "Healing Potions", 3, 0);
                    items.Add(item3);
                    ItemService.setItem(ref item4, "Adventuring Gear", 5, 1);
                    items.Add(item4);
                    break;
            };
        };
        channelerSecondItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item5, "Knife (hand)", 1, 1);
                    items.Add(item5);
                    break;
                case 2:
                    ItemService.setItem(ref item5, "Bo Staff (close, Two-Handed)", 1, 1);
                    items.Add(item5);
                    break;
            };
        };
        channelerThirdItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item6, "Smelling Salts", 1, 0);
                    items.Add(item6);
                    ItemService.setItem(ref item7, "Antitoxin", 1, 0);
                    items.Add(item7);
                    break;
                case 2:
                    ItemService.setItem(ref item6, "Pouch of coins", Roll(1, 10).Sum(), 0);
                    items.Add(item6);
                    break;
            };

            inventory.Items = items;
            character.Inventory = inventory;
        };


        necroFirstItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item4, "Leather Armor", 1, 1);
                    items.Add(item4);
                    character.ArmorRating = 1;
                    break;
                case 2:
                    ItemService.setItem(ref item4, "Healing Potions", 3, 0);
                    items.Add(item4);
                    ItemService.setItem(ref item5, "Adventuring Gear", 5, 1);
                    items.Add(item5);
                    break;
            };
        };
        necroSecondItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item6, "Bone Dagger (hand)", 1, 1);
                    items.Add(item6);
                    break;
                case 2:
                    ItemService.setItem(ref item6, "Skull Staff (close, Two-Handed)", 1, 1);
                    items.Add(item6);
                    break;
            };
        };
        necroThirdItemStep.OnValidResult += (result) =>
        {
            var jars = new HexedJars
            {
                Name = "Hexed Canopic Jars",
                Amount = 3,
                Weight = 1,
                Contents = new List<string>(),
            };

            switch (result)
            {
                case 1:
                    jars.Contents.Add("A one-armed Dwarf");
                    jars.Contents.Add("Empty");
                    jars.Contents.Add("Empty");
                    items.Add(jars);
                    break;
                case 2:
                    jars.Contents.Add("Someone you used to know");
                    jars.Contents.Add("Empty");
                    jars.Contents.Add("Empty");
                    items.Add(jars);
                    break;
                case 3:
                    jars.Contents.Add("A Skinless Hound");
                    jars.Contents.Add("Empty");
                    jars.Contents.Add("Empty");
                    items.Add(jars);
                    break;
            };

            inventory.Items = items;
            character.Inventory = inventory;
        };

        paladinFirstItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item4, "Halberd (reach, +1 damage, Two-Handed)", 1, 1);
                    items.Add(item4);
                    break;
                case 2:
                    ItemService.setItem(ref item4, "Long Sword (close, +1 damage)", 1, 1);
                    items.Add(item4);
                    ItemService.setItem(ref item5, "Shield", 1, 1);
                    character.ArmorRating += 1;
                    items.Add(item5);
                    break;
            };
        };
        paladinSecondItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item6, "Adventuring Gear", 5, 1);
                    items.Add(item6);
                    break;
                case 2:
                    items.Find(i => i.Name == "Dungeon Rations").Amount += 5;
                    ItemService.setItem(ref item6, "Healing Potions", 3, 0);
                    items.Add(item6);
                    break;
            };

            inventory.Items = items;
            character.Inventory = inventory;
        };

        rangerFirstItemStep.OnValidResult += (result) =>
        {
            ItemService.setItem(ref item4, "Hunter's Bow (near, far)", 1, 1);
            items.Add(item4);
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item5, "Short Sword (close)", 1, 1);
                    items.Add(item5);
                    break;
                case 2:
                    ItemService.setItem(ref item5, "Spear (reach)", 1, 1);
                    items.Add(item5);
                    break;
            };
        };
        rangerSecondItemStep.OnValidResult += (result) =>
        {
            ItemService.setItem(ref item6, "Adventuring Gear", 5, 1);
            items.Add(item6);
            switch (result)
            {
                case 1:
                    items.Find(i => i.Name == "Dungeon Rations").Amount += 5;
                    break;
                case 2:
                    items.Find(i => i.Name == "Bundle of Arrows").Amount += 3;
                    break;
            };

            inventory.Items = items;
            character.Inventory = inventory;
        };

        slayerFirstItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item4, "Dual Battle Axes (close, +1 damage)", 1, 1);
                    items.Add(item4);
                    break;
                case 2:
                    ItemService.setItem(ref item4, "Spear (close, thrown, near)", 1, 1);
                    items.Add(item4);
                    break;
                case 3:
                    ItemService.setItem(ref item4, "Claymore (close, +1 damage, Two-Handed)", 1, 1);
                    items.Add(item4);
                    break;
            };
        };
        slayerSecondItemStep.OnValidResult += (result) =>
        {
            switch (result)
            {
                case 1:
                    ItemService.setItem(ref item5, "Javelins (thrown, near)", 3, 1);
                    items.Add(item5);
                    break;
                case 2:
                    ItemService.setItem(ref item5, "Crossbow (near, +1 damage, reload)", 1, 1);
                    items.Add(item5);
                    break;
                case 3:
                    ItemService.setItem(ref item5, $"A Tropy from a recent kill worth {Roll(4, 8).Sum()} coins", 1, 1);
                    items.Add(item5);
                    break;
            };

            inventory.Items = items;
            character.Inventory = inventory;
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
                    editStep.setNextStep(classStep, true);
                    classStep.setNextStep(editStep, true);
                    character.ArmorRating = 0;
                    items.RemoveRange(0, items.Count);
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
                    statList.RemoveRange(0, 6);
                    break;
                case 5:
                    editStep.setNextStep(alignStep);
                    alignStep.setNextStep(editStep);
                    break;
                case 6:
                    switch (chosenClass.Type)
                    {
                        case ClassType.Channeler:
                            editStep.setNextStep(channelerFirstItemStep);
                            channelerThirdItemStep.setNextStep(editStep);
                            items.RemoveRange(2, items.Count - 2);
                            break;
                        case ClassType.Necromancer:
                            editStep.setNextStep(necroFirstItemStep);
                            necroThirdItemStep.setNextStep(editStep);
                            items.RemoveRange(3, items.Count - 3);
                            break;
                        case ClassType.Paladin:
                            editStep.setNextStep(paladinFirstItemStep);
                            paladinSecondItemStep.setNextStep(editStep);
                            items.RemoveRange(3, items.Count - 3);
                            items.Find(i => i.Name == "Dungeon Rations").Amount = 5;
                            character.ArmorRating -= 1;
                            break;
                        case ClassType.Ranger:
                            editStep.setNextStep(rangerFirstItemStep);
                            rangerSecondItemStep.setNextStep(editStep);
                            items.RemoveRange(3, items.Count - 3);
                            items.Find(i => i.Name == "Bundle of Arrows").Amount = 3;
                            items.Find(i => i.Name == "Dungeon Rations").Amount = 5;
                            break;
                        case ClassType.Slayer:
                            editStep.setNextStep(slayerFirstItemStep);
                            slayerSecondItemStep.setNextStep(editStep);
                            items.RemoveRange(3, items.Count - 3);
                            break;
                    };
                    break;
                case 7:
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

        embedFields.Add(new EmbedField(Name: "Total Armor", Value: character.ArmorRating.ToString()));

        embedFields.Add(new EmbedField(Name: "Inventory", Value: "--------------"));
        embedFields.AddRange(character.Inventory.Items.Select(s => new EmbedField(Name: s.Name, Value: s.Amount.ToString(), true)).ToArray());

        /*
        if(character.Class.Type == ClassType.Necromancer)
        {
            embedFields.Add(new EmbedField(Name: "Hexed Canopic Jars", Value: "--------------"));
            embedFields.AddRange(character.Inventory.Items.Select(s => new EmbedField(Name: s.Name, Value: s.Amount.ToString(), true)).ToArray());


        }*/
        

        var embed = new Embed 
        {
                Title = $"{character.Name}",
                Thumbnail = new EmbedThumbnail(ImageHelper.ClassToImgurLink(character.Class.Type)),
                Description = $"{character.Class.Type}: Level 1",
                Fields = embedFields,
                Colour = _feedbackService.Theme.Primary
        };

        await _feedbackService.SendPrivateEmbedAsync(userChannel, embed);


        //await _characterService.AddCharacterAsync(character);

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
        embedFields.Add(new EmbedField(Name: "Total Armor", Value: character.ArmorRating.ToString()));
        embedFields.Add(new EmbedField(Name: "Bonds", Value: 
            character.Bonds?.Count > 0 ? string.Join(',', character.Bonds.Select(b => b.TargetName)) : "None"));
        embedFields.Add(new EmbedField(Name: "Inventory", Value: "--------------"));
        embedFields.AddRange(character.Inventory.Items.Select(s => new EmbedField(Name: s.Name, Value: s.Amount.ToString(), true)).ToArray());

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"{character.Name}", 
                Thumbnail: new EmbedThumbnail(ImageHelper.ClassToImgurLink(character.Class.Type)),
                Description: $"{character.Class.Type.ToString()}: Level {character.Level}",
                Fields: embedFields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("bond")]
    public async Task<IResult> AddCharacterBondAsync(
        [Description("The user to bond with")]
        IUser user)
    {
        var character = await _characterService.GetCharacterFromUserAsync(_context.User);
        var bondTo = await _characterService.GetCharacterFromUserAsync(user);

        if (character is null || bondTo is null)
        {
            return await ReplyWithError("Either you do not have a character or the requested user does not have a character.");
        }

        await _characterService.BondWith(character, bondTo);
        
        return await _feedbackService.SendNeutralAsync(
            _context.ChannelID,
            $"You are now bonded with {bondTo.Name}"
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

    private List<int> Roll(int rollCount, int faces)
    {
        var results = new List<int>();

        for (var i = 0; i < rollCount; i++)
        {
            results.Add(_random.Next(1, faces));
        }

        return results;
    }
}