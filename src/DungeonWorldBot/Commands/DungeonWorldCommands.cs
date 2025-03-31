using DungeonWorldBot.Services;
using OneOf;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

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

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Campaign Characters", 
                Fields: characters.Select(c => new EmbedField(c.Class.Type.ToString(), c.Name)).ToList(),
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("world", "map")]
    public async Task<IResult> ShowMapAsync()
    {
        using var imageStream = new MemoryStream();
        using var image = await Image.LoadAsync("Assets/world.png");

        await image.SaveAsync(imageStream, PngFormat.Instance);
        
        imageStream.Seek(0, SeekOrigin.Begin);

        if (!_context.TryGetChannelID(out var channelId))
        {
            return Result.Success;
        }

        return await _channels.CreateMessageAsync(
            channelId,
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