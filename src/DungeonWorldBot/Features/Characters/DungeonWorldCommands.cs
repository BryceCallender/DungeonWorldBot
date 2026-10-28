using System.ComponentModel;
using OneOf;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Characters;

public class DungeonWorldCommands(FeedbackService feedbackService, ICharacterService characterService) : CommandGroup
{
    

    
    [Command("players")]
    [Description("List the characters in the campaign")]
    public async Task<IResult> ShowPlayersAsync()
    {
        var characters = await characterService.GetCharactersAsync();
        if (characters.Count == 0)
        {
            return await ReplyWithError("No characters are in the campaign");
        }

        return await feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "Campaign Characters", 
                Fields: characters.Select(c => new EmbedField(c.Class.Type.ToString(), c.Name)).ToList(),
                Colour: feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("world", "map")]
    [Description("Show the world map")]
    public async Task<IResult> ShowMapAsync()
    {
        await using var imageStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "world.png"));

        return await feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "World Map",
                Image: new EmbedImage("attachment://world.png"),
                Colour: feedbackService.Theme.Primary
            ),
            new FeedbackMessageOptions(Attachments: new OneOf<FileData, IPartialAttachment>[] { new FileData("world.png", imageStream) }),
            CancellationToken
        );
    }
    
    private async Task<Result> ReplyWithError(string error)
    {
        return (Result)await feedbackService.SendContextualErrorAsync
        (
            error,
            ct: CancellationToken
        );
    }
}