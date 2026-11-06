using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.GrimWorld;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Rules;

/// <summary>
/// How far away a Technique's target is, which decides whether Projectile (near) or Blast (close) deals the damage.
/// </summary>
public enum TechniqueRange
{
    Close,
    Near,
}

/// <summary>
/// A Technique's damage when it's channeled.
/// </summary>
/// <param name="Dice">e.g. "1d8" for Blast.</param>
/// <param name="Tag">The tag the damage comes from.</param>
/// <param name="Piercing">Armor it ignores, from the Piercing tag.</param>
public record TechniqueDamage(string Dice, TechniqueTag Tag, int Piercing);

/// <summary>
/// The Channeler's Techniques and the moves that change them.
/// </summary>
public static class TechniqueRules
{
    public const string Channel = "Channel";
    public const string Techniques = "Techniques";
    public const string SignatureTechnique = "Signature Technique";
    public const string Teleport = "Teleport";
    public const string AreaBurst = "Area Burst";
    public const string ThreadTheNeedle = "Thread the Needle";
    public const string Fissure = "Fissure";
    public const string ArmorOfTheElements = "Armor of the Elements";
    public const string DevastationWave = "Devastation Wave";

    /// <summary>
    /// Where Barrier armor comes from, so the next Channel can take it away.
    /// </summary>
    public const string BarrierSource = "Barrier";

    public const int BaseSlots = 3;
    public const int BaseTags = 2;
    public const int SignatureTags = 3;
    public const int Piercing = 2;
    public const int FissureDice = 3;

    public static ClassMove ChannelMove => Channeler.Sheet.FindMove(Channel)!;

    public static IReadOnlyList<TechniqueTag> AllTags { get; } =
        Enum.GetValues<TechniqueTag>().Where(t => t != TechniqueTag.None).ToList();

    public static bool CanChannel(Character character) => ClassSheetLibrary.Knows(character, Channel);

    /// <summary>
    /// How many Techniques the character can know. Signature Technique adds a slot.
    /// </summary>
    public static int MaxTechniques(Character character) => BaseSlots + (ClassSheetLibrary.Knows(character, SignatureTechnique) ? 1 : 0);

    public static int MaxTags(Technique technique) => technique.IsSignature ? SignatureTags : BaseTags;

    /// <summary>
    /// How many Thread the Needle d4s the character can add. Fissure allows three.
    /// </summary>
    public static int MaxThreadDice(Character character)
    {
        if (ClassSheetLibrary.Knows(character, Fissure))
            return FissureDice;
        return ClassSheetLibrary.Knows(character, ThreadTheNeedle) ? 1 : 0;
    }

    /// <summary>
    /// The armor Barrier gives until the next Channel. Armor of the Elements doubles it.
    /// </summary>
    public static int BarrierArmor(Character character) => ClassSheetLibrary.Knows(character, ArmorOfTheElements) ? 2 : 1;

    /// <summary>
    /// The tags the character can put on a Technique: the six everyone has, and those their moves unlock.
    /// </summary>
    public static IReadOnlyList<TechniqueTag> AvailableTags(Character character) =>
        AllTags.Where(t => UnlockedBy(t) is not { } move || ClassSheetLibrary.Knows(character, move)).ToList();

    public static IReadOnlyList<TechniqueTag> TagsOf(TechniqueTag tags) =>
        AllTags.Where(t => tags.HasFlag(t)).ToList();

    /// <summary>
    /// A tag typed by the player, e.g. "projectile".
    /// </summary>
    public static TechniqueTag? ParseTag(string? value) =>
        Enum.TryParse<TechniqueTag>(value?.Trim(), ignoreCase: true, out var tag) && AllTags.Contains(tag) ? tag : null;

    public static Technique? Find(Character character, string name) =>
        character.Techniques.FirstOrDefault(t => t.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static Technique? Signature(Character character) =>
        character.Techniques.FirstOrDefault(t => t.IsSignature);

    /// <summary>
    /// Why a Technique can't have these tags, or null if it can.
    /// </summary>
    public static string? WhyCannotHave(Character character, Technique technique, TechniqueTag tags)
    {
        var count = TagsOf(tags).Count;
        if (count != MaxTags(technique))
            return technique.IsSignature
                ? $"A Signature Technique has {SignatureTags} tags, not {count}."
                : $"A Technique has {BaseTags} tags, not {count}.";

        return LockedTag(character, tags) is { } locked
            ? $"The {locked} tag needs the {UnlockedBy(locked)} move."
            : null;
    }

    /// <summary>
    /// Why the character can't develop another Technique, or null if they can.
    /// </summary>
    /// <param name="signature">Whether it's their Signature Technique.</param>
    public static string? WhyCannotDevelop(Character character, string name, bool signature)
    {
        if (Find(character, name) is { } existing)
            return $"{character.Name} already knows {existing.Name}. Change it with /technique edit";

        if (signature && !ClassSheetLibrary.Knows(character, SignatureTechnique))
            return $"{character.Name} needs the {SignatureTechnique} move for a Technique with 3 tags.";

        if (signature && Signature(character) is { } current)
            return $"{current.Name} is already {character.Name}'s Signature Technique.";

        if (signature)
            return null;

        // Signature Technique's extra slot is for the 3-tag Technique only, so ordinary ones still stop at 3
        var ordinary = character.Techniques.Count(t => !t.IsSignature);
        return ordinary >= BaseSlots
            ? $"{character.Name} already knows {ordinary} Techniques, all they can hold. Replace one with /technique edit, or forget one with /technique forget"
            : null;
    }

    /// <summary>
    /// Why Devastation Wave can't add this tag to the Technique for one Channel, or null if it can.
    /// </summary>
    public static string? WhyCannotDevastate(Character character, Technique technique, TechniqueTag tag)
    {
        if (!ClassSheetLibrary.Knows(character, DevastationWave))
            return $"{character.Name} needs the {DevastationWave} move to add a fourth tag.";
        if (!technique.IsSignature)
            return $"{DevastationWave} only works on a Signature Technique, and {technique.Name} isn't one.";
        if (technique.Tags.HasFlag(tag))
            return $"{technique.Name} already has the {tag} tag.";

        return LockedTag(character, tag) is { } locked ? $"The {locked} tag needs the {UnlockedBy(locked)} move." : null;
    }

    /// <summary>
    /// The damage the Technique deals at this range, if any. A Technique with both Blast and Projectile needs the range
    /// to pick one; with only one of them, it's that one.
    /// </summary>
    public static TechniqueDamage? DamageOf(TechniqueTag tags, TechniqueRange? range)
    {
        var piercing = tags.HasFlag(TechniqueTag.Piercing) ? Piercing : 0;
        var blast = tags.HasFlag(TechniqueTag.Blast);
        var projectile = tags.HasFlag(TechniqueTag.Projectile);

        if (blast && (!projectile || range == TechniqueRange.Close))
            return new TechniqueDamage("1d8", TechniqueTag.Blast, piercing);
        if (projectile && (!blast || range == TechniqueRange.Near))
            return new TechniqueDamage("1d6", TechniqueTag.Projectile, piercing);
        return null;
    }

    /// <summary>
    /// Whether the Technique deals damage but needs a range to say which tag deals it.
    /// </summary>
    public static bool NeedsRange(TechniqueTag tags, TechniqueRange? range) =>
        range is null && tags.HasFlag(TechniqueTag.Blast) && tags.HasFlag(TechniqueTag.Projectile);

    public static string Describe(TechniqueTag tag, Character? character = null) => tag switch
    {
        TechniqueTag.Projectile => "1d6 damage if near",
        TechniqueTag.Barrier => $"+{(character is null ? 1 : BarrierArmor(character))} armor until your next Channel",
        TechniqueTag.Blast => "1d8 damage if close",
        TechniqueTag.Hazard => "an environmental effect",
        TechniqueTag.Piercing => $"{Piercing} piercing",
        TechniqueTag.Forceful => "knocks things back",
        TechniqueTag.Teleport => "you instantly move somewhere near",
        TechniqueTag.Burst => "affects up to 3 targets near each other, rolling any damage once for all of them",
        _ => tag.ToString(),
    };

    /// <summary>
    /// The Technique's tags, e.g. "Projectile · Forceful".
    /// </summary>
    public static string TagLine(TechniqueTag tags) => string.Join(" · ", TagsOf(tags));

    /// <summary>
    /// Each tag on its own line with what it does.
    /// </summary>
    public static string Describe(Technique technique, Character? character = null) =>
        string.Join('\n', TagsOf(technique.Tags).Select(t => $"• **{t}:** {Describe(t, character)}"));

    private static string? UnlockedBy(TechniqueTag tag) => tag switch
    {
        TechniqueTag.Teleport => Teleport,
        TechniqueTag.Burst => AreaBurst,
        _ => null,
    };

    // The first tag that needs a move the character doesn't know
    private static TechniqueTag? LockedTag(Character character, TechniqueTag tags)
    {
        var locked = TagsOf(tags).FirstOrDefault(t => UnlockedBy(t) is { } move && !ClassSheetLibrary.Knows(character, move));
        return locked == TechniqueTag.None ? null : locked;
    }
}
