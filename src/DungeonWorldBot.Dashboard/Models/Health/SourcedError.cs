using DungeonWorldBot.Infrastructure.Telemetry;

namespace DungeonWorldBot.Dashboard.Models.Health;

/// <summary>
/// An error, and which of the two logged it: "Bot" or "Dashboard".
/// </summary>
public record SourcedError(string From, LoggedError Error);
