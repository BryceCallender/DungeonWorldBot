namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// Something that went wrong, as it was logged.
/// </summary>
/// <param name="Category">Where it was logged from, e.g. "DungeonWorldBot.Features.Encounters.MoveRequestWorker".</param>
/// <param name="Exception">The exception's type and message, if there was one.</param>
public record LoggedError(DateTimeOffset At, string Level, string Category, string Message, string? Exception);
