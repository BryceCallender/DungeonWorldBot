namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <param name="Nemesis">The kind that's hurt the party most, if any has.</param>
public record Bestiary(IReadOnlyList<BestiaryEntry> Entries, BestiaryEntry? Nemesis);
