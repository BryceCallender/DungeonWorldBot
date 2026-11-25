using System.Text.Json.Serialization;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Features.Subclasses.Homebrew;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Classes;

/// <summary>
/// Something only some characters have, from their class, moves or subclass, that a group of the bot's commands is
/// for, like the Druid's Shapeshifter for /shapeshift. Mark the group with <see cref="ForClassMechanicAttribute"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClassMechanic>))]
public enum ClassMechanic
{
    /// <summary>The Druid's Shapeshifter, or anyone who's learned it.</summary>
    Shapeshifting,

    /// <summary>Casting spells from any list: Wizards, Clerics, and anyone with a move that grants spells.</summary>
    Spellcasting,

    /// <summary>The Channeler's Techniques, through Channel.</summary>
    Techniques,

    /// <summary>The Battlemaster's Tactics.</summary>
    Tactics,

    /// <summary>The Immolator's Burning Brand.</summary>
    BurningBrand,

    /// <summary>The Necromancer's Hexed Canopic Jars, through Corpse Collector.</summary>
    HexedJars,

    /// <summary>The Shaman's Spirit Catcher.</summary>
    SpiritCatcher,

    /// <summary>The Paladin's Quest.</summary>
    Quest,

    /// <summary>The Lantern's little light.</summary>
    Light,

    ChampionOfLuna,
    TacticalGuardian,
    CircleOfTheEclipse,
}

/// <summary>
/// Marks a group of slash commands (or a single command) as only for characters with a <see cref="ClassMechanic"/>, so
/// the dashboard offers them only to those it applies to. Commands without it are for everyone.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ForClassMechanicAttribute(ClassMechanic mechanic) : Attribute
{
    public ClassMechanic Mechanic { get; } = mechanic;
}

public static class ClassMechanics
{
    /// <summary>
    /// Whether the character has it, by the same rules as the commands it's for.
    /// </summary>
    public static bool Has(this Character character, ClassMechanic mechanic) => mechanic switch
    {
        ClassMechanic.Shapeshifting => ShapeshiftRules.CanShapeshift(character),
        ClassMechanic.Spellcasting => SpellCasting.Access(character).Count > 0,
        ClassMechanic.Techniques => TechniqueRules.CanChannel(character),
        ClassMechanic.Tactics => TacticRules.UsesTactics(character),
        ClassMechanic.BurningBrand => BrandRules.CanConjure(character),
        ClassMechanic.HexedJars => JarRules.CanCollect(character),
        ClassMechanic.SpiritCatcher => SpiritCatcherRules.Catcher(character) is not null,
        ClassMechanic.Quest => ClassSheetLibrary.Knows(character, QuestRules.Quest),
        ClassMechanic.Light => character.Class.Type == ClassType.Lantern,
        ClassMechanic.ChampionOfLuna => InSubclass(character, ChampionOfLuna.Name),
        ClassMechanic.TacticalGuardian => InSubclass(character, TacticalGuardian.Name),
        ClassMechanic.CircleOfTheEclipse => InSubclass(character, CircleOfTheEclipse.Name),
        _ => false,
    };

    private static bool InSubclass(Character character, string subclass) =>
        SubclassLibrary.Of(character)?.Name.Equals(subclass, StringComparison.OrdinalIgnoreCase) == true;
}
