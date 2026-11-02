using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.GrimWorld;

namespace DungeonWorldBot.Rules;

/// <summary>
/// The Shaman's Spirit Catcher totem and the one spirit it can hold.
/// </summary>
/// <remarks>
/// The caught spirit is kept as the Spirit Catcher ability's description: "Holding: {spirit}", then the effect the GM
/// gives it on the next line. Releasing it puts the move's own text back.
/// </remarks>
public static class SpiritCatcherRules
{
    public const string SpiritCatcher = "Spirit Catcher";
    public const string HoldingPrefix = "Holding: ";

    public static ClassMove Move => Shaman.Sheet.FindMove(SpiritCatcher)!;

    public static Ability? Catcher(Character character) =>
        character.Abilities?.FirstOrDefault(a => a.Name.Equals(SpiritCatcher, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The spirit in the totem, e.g. "a dire wolf", or null when it's empty.
    /// </summary>
    public static string? Spirit(Character character) =>
        Catcher(character)?.Description is { } description && description.StartsWith(HoldingPrefix)
            ? description.Split('\n')[0][HoldingPrefix.Length..]
            : null;

    /// <summary>
    /// What the spirit does when released, once the GM has said.
    /// </summary>
    public static string? Effect(Character character) =>
        Spirit(character) is not null && Catcher(character)!.Description!.Split('\n', 2) is [_, var effect] ? effect : null;

    public static string Describe(string spirit, string? effect) =>
        string.IsNullOrWhiteSpace(effect) ? HoldingPrefix + spirit : $"{HoldingPrefix}{spirit}\n{effect.Trim()}";

    /// <summary>
    /// The totem for the character sheet.
    /// </summary>
    public static string Status(Character character)
    {
        if (Spirit(character) is not { } spirit)
            return "The Spirit Catcher is empty.";

        return Effect(character) is { } effect
            ? $"🪶 Holding **{spirit}**\nWhen released: {effect}"
            : $"🪶 Holding **{spirit}**\n*Ask the GM what it does when released, then add it with /spirit effect*";
    }
}
