using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Helpers;

public static class ImageHelper
{
    public static string ClassToImgurLink(ClassType type)
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
            _ => ""
        };
    }
}