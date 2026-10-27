namespace DungeonWorldBot.Dashboard.Models.Party;

public record ArmorView(int Physical, int Magical, int Divine, int BasePhysical, int BaseMagical, int BaseDivine, IReadOnlyList<string> Bonuses);
