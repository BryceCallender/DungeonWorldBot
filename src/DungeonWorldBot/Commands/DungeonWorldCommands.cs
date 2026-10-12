using System.ComponentModel;
using DungeonWorldBot.Services;
using OneOf;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace DungeonWorldBot.Commands;

public class DungeonWorldCommands : CommandGroup
{
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    
    public DungeonWorldCommands(
        FeedbackService feedbackService,
        ICharacterService characterService)
    {
        _feedbackService = feedbackService;
        _characterService = characterService;
    }
    
    [Command("players")]
    [Description("List the characters in the campaign")]
    public async Task<IResult> ShowPlayersAsync()
    {
        var characters = await _characterService.GetCharactersAsync();
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
    [Description("Show the world map")]
    public async Task<IResult> ShowMapAsync()
    {
        using var imageStream = new MemoryStream();
        using var image = await Image.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "world.png"), CancellationToken);

        await image.SaveAsync(imageStream, PngFormat.Instance, CancellationToken);
        imageStream.Seek(0, SeekOrigin.Begin);

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "World Map",
                Image: new EmbedImage("attachment://world.png"),
                Colour: _feedbackService.Theme.Primary
            ),
            new FeedbackMessageOptions(Attachments: new OneOf<FileData, IPartialAttachment>[]
            {
                new FileData("world.png", imageStream)
            }),
            CancellationToken
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