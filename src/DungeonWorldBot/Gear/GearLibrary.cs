using DungeonWorldBot.Gear.Homebrew;

namespace DungeonWorldBot.Gear;

/// <summary>
/// Every piece of custom gear. Add homebrew gear by giving it a file in Homebrew/ and listing it here.
/// </summary>
public static class GearLibrary
{
    public static readonly IReadOnlyList<CustomGear> All =
    [
        GemAspectedBow.Gear,
    ];

    public static CustomGear? Find(string name) =>
        All.FirstOrDefault(g => g.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Every counter any gear keeps, for looking one up by name.
    /// </summary>
    public static IEnumerable<GearCounter> Counters =>
        All.SelectMany(g => g.Modes).Select(m => m.Counter).OfType<GearCounter>();

    public static IEnumerable<(CustomGear Gear, GearMode Mode, GearAction Action)> Actions =>
        All.SelectMany(g => g.Modes.Where(m => m.Action is not null).Select(m => (g, m, m.Action!)));
}
