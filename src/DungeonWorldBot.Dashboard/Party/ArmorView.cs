namespace DungeonWorldBot.Dashboard.Party;

public sealed record ArmorView(int Physical, int Magical, int Divine, int BasePhysical, int BaseMagical, int BaseDivine, IReadOnlyList<string> Bonuses);
