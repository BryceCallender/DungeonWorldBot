using System.Globalization;

namespace DungeonWorldBot.Dashboard.Helpers;

/// <summary>
/// Numbers written into SVG attributes and inline styles, which need a dot for decimals whatever the server's culture.
/// </summary>
public static class Markup
{
    public static string Num(double value, string format = "0.#") => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// How full a bar is, as a CSS percentage from 0 to 100.
    /// </summary>
    public static string BarWidth(int value, int max) => Num(max <= 0 ? 0 : Math.Clamp(value * 100.0 / max, 0, 100));
}
