using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// Changes to rows the bot can change at the same moment, like an enemy's HP during a fight (see the HP checks in
/// DungeonWorldContext). One that loses the race is worked out again from the fresh row, so a hit in game chat and one
/// on the dashboard both count.
/// </summary>
public static class Conflicts
{
    private const int Tries = 3;

    public const string StillChanging = "It kept changing while this was saved. Check it and try again.";

    /// <param name="change">Opens its own context each time, so every try reads the row as it is now.</param>
    /// <returns>Why it couldn't, or null once it has.</returns>
    public static async Task<string?> RetryAsync(Func<Task<string?>> change)
    {
        for (var tries = 1; tries <= Tries; tries++)
        {
            try
            {
                return await change();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else saved it first; the next try starts from what they saved
            }
        }

        return StillChanging;
    }
}
