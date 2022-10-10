using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services;
using EnumsNET;
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

namespace DungeonWorldBot.Commands;

[Group("character")]
public class CharacterCommand : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IDiscordRestGuildAPI _guildAPI;
    
    public CharacterCommand(
        ICommandContext context, 
        FeedbackService feedbackService, 
        ICharacterService characterService,
        IDiscordRestGuildAPI guildAPI)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _guildAPI = guildAPI;
    }
    
    [Command("create")]
    public async Task<IResult> CreateCharacterAsync()
    {
        await _characterService.AddCharacterAsync(new Character
        {
            ID = _context.User.ID,
            Name = "Zephyr",
            Alignment = Alignment.LawfulGood,
            ArmorRating = 0,
            Class = new Class
            {
                Type = ClassType.Ranger
            },
            Health = new Health
            {
                CurrentHP = 10,
                MaxHP = 10
            },
            Level = 1,
            Stats = new List<Stat>
            {
                new() { StatType = StatType.Dex, Value = 16 },
                new() { StatType = StatType.Cha, Value = 15 },
                new() { StatType = StatType.Con, Value = 13 },
                new() { StatType = StatType.Str, Value = 12 },
                new() { StatType = StatType.Wis, Value = 9  },
                new() { StatType = StatType.Int, Value = 8  }
            }
        });
        
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
            "No character exists. Try making one with !create",
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