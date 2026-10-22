using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Subclasses;

namespace DungeonWorldBot.Dashboard.Reference;

/// <summary>
/// Addresses and summaries for the class, subclass and spell reference pages, read from the bot's own rules.
/// </summary>
public static partial class Lookup
{
    public static string ClassUrl(ClassType type, string? forCharacter = null) =>
        $"classes/{type}" + (forCharacter is null ? "" : $"?for={forCharacter}");

    public static string SubclassUrl(Subclass subclass, string? forCharacter = null) =>
        $"subclasses/{Slug(subclass.Name)}" + (forCharacter is null ? "" : $"?for={forCharacter}");

    public static string SpellsUrl(string? forCharacter = null) =>
        "spells" + (forCharacter is null ? "" : $"?for={forCharacter}");

    /// <summary>
    /// The inline style that gives a class its color from its sheet, picked up as --class by anything marked "classed".
    /// </summary>
    public static string ColorOf(ClassType type) =>
        ClassSheetLibrary.For(type)?.Color is { } color ? $"--class-light: {color.Light}; --class-dark: {color.Dark};" : "";

    public static ClassSheet? FindClass(string name) =>
        Enum.TryParse<ClassType>(name, ignoreCase: true, out var type) ? ClassSheetLibrary.For(type) : null;

    public static Subclass? FindSubclass(string slug) =>
        SubclassLibrary.All.FirstOrDefault(s => Slug(s.Name) == slug.ToLowerInvariant());

    /// <summary>
    /// e.g. "Champion of Luna" → "champion-of-luna", "Mr. Hatter" → "mr-hatter".
    /// </summary>
    public static string Slug(string name) =>
        string.Join('-', NonWord().Split(name.ToLowerInvariant()).Where(part => part.Length > 0));

    [System.Text.RegularExpressions.GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial System.Text.RegularExpressions.Regex NonWord();

    /// <summary>
    /// The class's damage die and its HP before Constitution, as the bot works them out.
    /// </summary>
    public static (string Damage, int BaseHP) Basics(ClassType type)
    {
        var rules = new Class { Type = type };
        return (rules.Damage, rules.HealthModifier);
    }

    public static ClassKit? Kit(ClassType type) => ClassKits.All.FirstOrDefault(k => k.Type == type);
}
