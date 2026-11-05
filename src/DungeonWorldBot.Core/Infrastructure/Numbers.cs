namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// Numbers as the bot's messages show them.
/// </summary>
public static class Numbers
{
    /// <summary>
    /// A modifier with its sign, e.g. "+2" or "-1".
    /// </summary>
    public static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
}
