using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.DungeonWorld;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Rules;

/// <summary>
/// The Immolator's Burning Brand: a weapon of pure flame, kept in their inventory while it burns.
/// </summary>
public static class BrandRules
{
    public const string BurningBrand = "Burning Brand";
    public const string UsesName = "Burning Brand Uses";
    public const int Uses = 3;

    public const string PlusOneDamage = "+1 damage";
    public const string RemoveDangerous = "Remove the dangerous tag";

    public static ClassMove Move => Immolator.Sheet.FindMove(BurningBrand)!;

    public static ClassResource UsesResource => Immolator.Sheet.FindResource(UsesName)!;

    /// <summary>
    /// The tags to choose from when conjuring it.
    /// </summary>
    public static IReadOnlyList<string> Options => Move.Roll!.Options!;

    public static bool CanConjure(Character character) =>
        ClassSheetLibrary.Knows(character, BurningBrand);

    /// <summary>
    /// How many tags the roll lets them choose: two on a 10+, one on a 7-9.
    /// </summary>
    public static int TagsFor(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => 2,
        MoveOutcome.WeakHit => 1,
        _ => 0,
    };

    public static string? FindOption(string value) =>
        Options.FirstOrDefault(o => o.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    public static Weapon? Brand(Character character) =>
        character.Inventory?.Items?.OfType<Weapon>().FirstOrDefault(w => w.Name.StartsWith(BurningBrand, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The brand as an item: its tags in its name, e.g. "Burning Brand (fiery, touch, dangerous, hand)", and its damage.
    /// </summary>
    public static Weapon Forge(IReadOnlyList<string> chosen)
    {
        List<string> tags = ["fiery", "touch"];
        if (!chosen.Contains(RemoveDangerous))
            tags.Add("dangerous");
        tags.AddRange(chosen.Where(c => c is not PlusOneDamage and not RemoveDangerous));

        var damage = chosen.Contains(PlusOneDamage) ? 1 : 0;
        if (damage > 0)
            tags.Add(PlusOneDamage);

        return new Weapon { Name = $"{BurningBrand} ({string.Join(", ", tags)})", Damage = damage, Amount = 1, Weight = 0 };
    }
}
