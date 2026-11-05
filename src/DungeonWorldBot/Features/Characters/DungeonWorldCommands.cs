using System.ComponentModel;
using DungeonWorldBot.Infrastructure;
using OneOf;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Characters;

public class DungeonWorldCommands(ICommandContext context, FeedbackService feedbackService, ICharacterService characterService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    [Command("players")]
    [Description("List the characters in the campaign")]
    public async Task<IResult> ShowPlayersAsync()
    {
        var characters = await characterService.GetCharacterNamesAsync();
        if (characters.Count == 0)
            return await ReplyWithErrorAsync("No characters are in the campaign");

        var description = characters.Count > MaxEmbedFields ? $"Showing {MaxEmbedFields} of {characters.Count} characters" : string.Empty;
        return await ReplyAsync("Campaign Characters", description,
            characters.Take(MaxEmbedFields).Select(c => (IEmbedField)new EmbedField(c.Class.ToString(), c.Name)).ToList());
    }

    [Command("world", "map")]
    [Description("Show the world map")]
    public async Task<IResult> ShowMapAsync()
    {
        await using var imageStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", "world.png"));

        return await ReplyWithEmbedAsync(
            new Embed(
                Title: "World Map",
                Image: new EmbedImage("attachment://world.png"),
                Colour: _feedbackService.Theme.Primary
            ),
            new FeedbackMessageOptions(Attachments: new OneOf<FileData, IPartialAttachment>[] { new FileData("world.png", imageStream) })
        );
    }
}
