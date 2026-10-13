using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A class move, spell, or feature a character has, optionally with limited uses or hold to track.
/// </summary>
public class Ability
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>
    /// How many uses (or hold) the ability has when fully restored, or null if it isn't limited.
    /// </summary>
    public int? MaxUses { get; set; }
    public int Uses { get; set; }

    public bool IsLimited => MaxUses is not null;

    public string ToDisplay() => IsLimited ? $"{Name} ({Uses}/{MaxUses})" : Name;
}
