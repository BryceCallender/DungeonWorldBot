using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A shape a shapeshifter (like a Druid) can take: a species from their land or one whose essence they've studied.
/// </summary>
public class Transformation
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// The form's natural abilities and weaknesses.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The moves the GM gave the form, which cost 1 hold each.
    /// </summary>
    public string? Moves { get; set; }

    /// <summary>
    /// Whether the character is in this form right now. More than one can be active when shapes are merged (Chimera).
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// How many times the character has taken this form.
    /// </summary>
    public int TimesTaken { get; set; }
}
