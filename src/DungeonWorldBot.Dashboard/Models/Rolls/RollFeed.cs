namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Latest">The newest roll's message ID, to pass back as <c>after</c> next time.</param>
/// <param name="Rolls">Rolls newer than <c>after</c>, oldest first.</param>
public record RollFeed(string? Latest, IReadOnlyList<FeedRoll> Rolls);
