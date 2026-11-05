using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Data;

public static class DbUpdateExceptions
{
    // SQLITE_CONSTRAINT_UNIQUE
    private const int UniqueConstraint = 2067;

    /// <summary>
    /// Whether the save was refused by a unique index, as when the bot and the dashboard add the same thing at once.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException e) =>
        e.InnerException is SqliteException { SqliteExtendedErrorCode: UniqueConstraint };
}
