using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Helpers;

public static class ImageHelper
{
    /// <summary>
    /// The class's picture for an embed, or nothing if the class doesn't have one (Discord rejects empty image links).
    /// </summary>
    public static Optional<IEmbedThumbnail> ClassThumbnail(ClassType type) =>
        ClassToImgurLink(type) is { } link ? new EmbedThumbnail(link) : default(Optional<IEmbedThumbnail>);

    public static string? ClassToImgurLink(ClassType type)
    {
        return type switch
        {
            ClassType.Ranger => "https://imgur.com/UjUwwgL.png",
            ClassType.Paladin => "https://imgur.com/YsGk6lh.png",
            ClassType.Channeler => "https://imgur.com/4yUbQDo.png",
            ClassType.Necromancer => "https://imgur.com/bhYjiUH.png",
            ClassType.Slayer => "https://imgur.com/xmsUnY8.png",
            ClassType.Battlemaster => "https://imgur.com/0VLMlA7.png",
            ClassType.Druid => "https://imgur.com/nsGQTuy.png",
            _ => null
        };
    }
}