using DungeonWorldBot.Services;
using OneOf;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace DungeonWorldBot.Commands;

public class DungeonWorldCommands : CommandGroup
{
    private readonly ICommandContext _context;
    private readonly IDiscordRestChannelAPI _channels;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    
    public DungeonWorldCommands(
        ICommandContext context,
        IDiscordRestChannelAPI channels,
        FeedbackService feedbackService,
        ICharacterService characterService)
    {
        _context = context;
        _channels = channels;
        _feedbackService = feedbackService;
        _characterService = characterService;
    }
    
    [Command("players")]
    public async Task<IResult> ShowPlayersAsync()
    {
        var characters = _characterService.GetCharacters();
        if (characters.Count == 0)
        {
            return await ReplyWithError("No characters are in the campaign");
        }
        
        return Result.FromSuccess();
    }

    [Command("world", "map")]
    public async Task<IResult> ShowMapAsync()
    {
        using var imageStream = new MemoryStream();
        using var image = Image.Load("Assets/world.png", out var format);

        await image.SaveAsync(imageStream, format);
        
        imageStream.Seek(0, SeekOrigin.Begin);

        return await _channels.CreateMessageAsync(
            _context.ChannelID,
            attachments: new OneOf<FileData, IPartialAttachment>[]
            {
                new FileData("world.png", imageStream)
            }
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