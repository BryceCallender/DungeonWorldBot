namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// How often a slash command has run since the bot started, and how often it didn't work.
/// </summary>
/// <param name="Command">Its full name, e.g. "dm npc add".</param>
public record CommandCount(string Command, int Runs, int Failed, double AverageMilliseconds);
