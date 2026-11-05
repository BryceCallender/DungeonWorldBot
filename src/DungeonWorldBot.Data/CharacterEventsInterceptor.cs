using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DungeonWorldBot.Data;

/// <summary>
/// Adds the <see cref="CharacterEvents"/> for each save, before it's written, so they land in the same save.
/// </summary>
internal sealed class CharacterEventsInterceptor : SaveChangesInterceptor
{
    public static readonly CharacterEventsInterceptor Instance = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Record(DbContext? context)
    {
        if (context is DungeonWorldContext game)
            CharacterEvents.Record(game);
    }
}
