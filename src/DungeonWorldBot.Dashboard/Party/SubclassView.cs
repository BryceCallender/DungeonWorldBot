namespace DungeonWorldBot.Dashboard.Party;

public sealed record SubclassView(string Name, string Title, string? Status, IReadOnlyList<SubclassMoveLine> Moves);
