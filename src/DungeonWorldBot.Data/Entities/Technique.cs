using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A way a Channeler has developed to shape their element, named by their player and made of tags like Projectile or Barrier.
/// </summary>
public class Technique
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; } = null!;

    public string Name { get; set; } = null!;

    public TechniqueTag Tags { get; set; }

    /// <summary>
    /// The extra Technique from the Signature Technique move, which can hold 3 tags.
    /// </summary>
    public bool IsSignature { get; set; }
}
