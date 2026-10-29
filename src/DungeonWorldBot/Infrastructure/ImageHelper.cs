using DungeonWorldBot.Data.Entities;
using OneOf;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Rest.Core;

namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// A class's icon for an embed: the thumbnail pointing at the attached picture, and the options that attach it.
/// Both go on the same message, or Discord shows no thumbnail.
/// </summary>
public record ClassIcon(Optional<IEmbedThumbnail> Thumbnail, FeedbackMessageOptions? Options);

public static class ImageHelper
{
    // Each class's icon from its sheet, on its color, sent along with the embed rather than linked from elsewhere
    private static readonly string IconFolder = Path.Combine(AppContext.BaseDirectory, "Assets", "Classes");

    /// <summary>
    /// The class's icon, or nothing if it has no picture (Discord rejects links to attachments that aren't there).
    /// </summary>
    public static ClassIcon ClassIcon(ClassType type)
    {
        var fileName = $"{type}.png";
        var path = Path.Combine(IconFolder, fileName);
        if (!File.Exists(path))
            return new ClassIcon(default, null);

        // Read in full, since the message is sent after this returns and nothing would close an open file
        var picture = new MemoryStream(File.ReadAllBytes(path));
        return new ClassIcon(
            new EmbedThumbnail($"attachment://{fileName}"),
            new FeedbackMessageOptions(Attachments: new OneOf<FileData, IPartialAttachment>[] { new FileData(fileName, picture) }));
    }
}
