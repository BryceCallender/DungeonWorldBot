namespace DungeonWorldBot.Dashboard.Models.Party;

public record SubclassView(string Name, string Title, string? Status, IReadOnlyList<SubclassMoveLine> Moves);
