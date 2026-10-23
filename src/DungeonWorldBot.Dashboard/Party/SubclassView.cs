namespace DungeonWorldBot.Dashboard.Party;

public record SubclassView(string Name, string Title, string? Status, IReadOnlyList<SubclassMoveLine> Moves);
