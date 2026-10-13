using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.Subclasses.Homebrew;

public enum EclipseAspect
{
    Sunward,
    Moonward,
}

/// <summary>
/// Circle of the Eclipse: a homebrew Druid subclass from the lost city of Selunaris. Each move gives a Sunward and a
/// Moonward version; the character uses whichever matches their current aspect.
/// </summary>
public static class CircleOfTheEclipse
{
    public const string Name = "Circle of the Eclipse";
    public const string AspectShift = "Aspect Shift";
    public const string EclipseTouched = "Eclipse-Touched";
    public const string SolarMend = "Solar Mend";
    public const string PhaseDrift = "Phase Drift";
    public const string BlindingRadiance = "Blinding Radiance";
    public const string ShadowStep = "Shadow Step";
    public const string EclipsesEmbrace = "Eclipse's Embrace";
    public const string TotalEclipse = "Total Eclipse";

    /// <summary>
    /// The ability holding the character's current aspect.
    /// </summary>
    public const string CurrentAspect = "Eclipse Aspect";

    public static readonly Subclass Subclass = new(
        Name,
        "Attuned to the Harmony of Light and Shadow",
        "Homebrew",
        [ClassType.Druid],
        "Forged in the lost city of Selunaris under the Moon Goddess's gaze. Balance the raw energy of the sun with the mystery of the moon, " +
        "shifting between the Sunward and Moonward aspects to keep the cosmic rhythm.",
        [
            new(1, new(AspectShift,
                "You're attuned to either the **Sunward** or **Moonward** aspect. You may change aspect at dawn or dusk, or mid-session by spending " +
                "1 Hold from shapeshifting or succeeding on Defy Danger (WIS) in a moment of intense stress (GM's call). When you change aspect, a " +
                "visible eclipse briefly flashes in your eyes or around your form.")),
            new(1, new(EclipseTouched,
                "Your magic carries the essence of the moon and sun. You may choose radiant or shadow as an element when casting spells or " +
                "shapeshifting. This may interact with celestial artifacts, magic effects, or specific enemies.")),
            new(1, Rolled(SolarMend, StatType.Wis,
                "🌞 **Sunward only.** When you touch a creature to heal",
                "Heal 1d8 + WIS HP and give them +1 forward.",
                "Heal 1d6, but they take 1d4 burn damage at the start of their next turn.",
                "They take 1d6 burn damage, and you can't use this healing again until you rest.")),
            new(1, new(PhaseDrift,
                "🌑 **Moonward only.** When you spend 1 Hold from shapeshifting, you may instead become phased until you take an action. While " +
                "phased you can move through thin barriers or creatures, and you're immune to physical and magical effects but can't interact " +
                "with the world. You may walk, observe, or speak. The effect ends when you act (e.g. attack, cast, or touch something with intent).")),
            new(3, Rolled(BlindingRadiance, StatType.Wis,
                "🌞 **Sunward only.** When you touch a creature",
                "Deal 1d8 radiant damage, and the target must make a CON save or be blinded until the end of their next turn.",
                "Deal 1d6 radiant damage, and they're dazzled (-1 forward).",
                "You take 1d4 damage and are briefly blinded until your next turn.",
                note: "This is magical damage and ignores physical armor.")),
            new(3, new(ShadowStep,
                "🌘 **Moonward only.** You may teleport up to 30 ft as a bonus action or movement, but only into dim light or darkness. " +
                "Uses per day: your WIS modifier (min 1). Stepping through moonlight (natural or magical) restores one use.")),
            new(6, new(EclipsesEmbrace,
                "When a creature you can see makes an attack roll, use your aspect's power.\n" +
                "🌞 **Solar Reversal:** When an enemy within range attacks, force the attack to be made with disadvantage. If the enemy is in direct " +
                "sunlight, they also take 1d8 radiant damage.\n" +
                "🌘 **Lunar Balance:** When an ally within range is attacked, grant them advantage on their next saving throw or attack roll. If " +
                "the ally is in dim light or darkness, their next attack also deals +1d6 necrotic damage.")),
            new(9, new(TotalEclipse,
                "Once per long rest, invoke Total Eclipse as a free action. You count as both Sunward and Moonward, and your aspect moves are empowered.\n" +
                "**Enemies** in the area take 1d8 radiant and 1d8 necrotic damage at the start of each of their turns, must Defy Danger (CON) or be " +
                "blinded and frightened until the end of their turn, and take -1 ongoing to cast spells or make precise ranged attacks.\n" +
                "**Allies** in the zone gain +1 armor and +1 forward to attack, and at the start of their turn choose: heal 1d6, or become " +
                "intangible (like Phase Drift) until they act.\n" +
                "🌞 Healing restores the maximum. Blinding Radiance hits every enemy in Close range. Radiant damage ignites enemies (1 ongoing fire damage for 3 rounds).\n" +
                "🌘 Phase Drift passes through solid terrain. Shadow Step leaves a phantom decoy that distracts an enemy until it's attacked.")),
        ])
    {
        Status = c => AspectOf(c) is { } aspect
            ? $"{Icon(aspect)} **{aspect}**"
            : "No aspect chosen. Use /eclipse aspect",
        StateAbilities = [CurrentAspect],
        CommandHint = "Choose your aspect with /eclipse aspect. Roll Solar Mend and Blinding Radiance with /move class.",
    };

    public static EclipseAspect? AspectOf(Character character)
    {
        var ability = character.Abilities?.FirstOrDefault(a => a.Name.Equals(CurrentAspect, StringComparison.OrdinalIgnoreCase));
        return Enum.TryParse<EclipseAspect>(ability?.Description?.Trim(), ignoreCase: true, out var aspect) ? aspect : null;
    }

    public static string Icon(EclipseAspect aspect) => aspect == EclipseAspect.Sunward ? "🌞" : "🌑";

    /// <summary>
    /// The moves the aspect lets the character use, at their level.
    /// </summary>
    public static IEnumerable<string> AspectMoves(EclipseAspect aspect, int level) =>
        (aspect == EclipseAspect.Sunward
            ? new[] { (1, SolarMend), (3, BlindingRadiance), (6, "Solar Reversal") }
            : new[] { (1, PhaseDrift), (3, ShadowStep), (6, "Lunar Balance") })
        .Where(m => m.Item1 <= level)
        .Select(m => m.Item2);
}
