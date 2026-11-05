namespace DungeonWorldBot.Features.Spells;

public enum SpellList
{
    Cleric,
    Wizard,
}

/// <param name="Level">The spell's level, or 0 for rotes and cantrips, which are always available and never count against the limit.</param>
/// <param name="Tags">Tags like Ongoing or Evocation.</param>
public record Spell(string Name, SpellList List, int Level, string Description, params string[] Tags)
{
    public bool IsFree => Level == 0;

    public bool IsOngoing => Tags.Contains("Ongoing", StringComparer.OrdinalIgnoreCase);

    public string LevelLabel => Level switch
    {
        0 when List == SpellList.Cleric => "Rote",
        0 => "Cantrip",
        _ => $"Level {Level}",
    };

    /// <summary>
    /// e.g. "Level 1 cleric spell, ongoing".
    /// </summary>
    public string Heading =>
        $"{LevelLabel} {List.ToString().ToLower()} spell" + (Tags.Length > 0 ? $", {string.Join(", ", Tags.Select(t => t.ToLower()))}" : string.Empty);

    public string Qualified => $"{Name} ({List})";
}
