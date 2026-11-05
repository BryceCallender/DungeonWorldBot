namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// How long a move asked for from the dashboard (see <see cref="Data.Entities.MoveRequest"/>) waits for the bot.
/// </summary>
public static class MoveRequestRules
{
    /// <summary>
    /// Moves asked for longer ago than this, as while the bot was off, are dropped rather than all landing at once.
    /// </summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(1);

    public const string TooLate = "The bot wasn't around to roll it in time. Try again.";
}
