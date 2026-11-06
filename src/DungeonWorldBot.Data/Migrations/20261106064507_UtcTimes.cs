using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class UtcTimes : Migration
    {
        // Every time stored as text, which UtcDateTimeOffsetConverter now writes as UTC without an offset
        private static readonly TimeColumn[] Times =
        [
            new("CharacterEvents", "At"),
            new("EncounterActions", "At"),
            new("Encounters", "StartedAt"),
            new("Encounters", "EndedAt"),
            new("Encounters", "AnnouncedAt"),
            new("Encounters", "EndAnnouncedAt"),
            new("MoveRequests", "RequestedAt"),
            new("MoveRequests", "ResolvedAt"),
            new("Sessions", "StartedAt"),
            new("Sessions", "EndedAt"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "2026-11-04 03:01:02.5+00:00" becomes "2026-11-04 03:01:02.5". The bot only ever saved UTC, so the
            // offset is just dropped; anything else is turned into UTC by SQLite, to the millisecond. Times already
            // without an offset are left alone, so this is safe to run on a database that's partly converted.
            foreach (var time in Times)
                migrationBuilder.Sql($"""
                    UPDATE "{time.Table}" SET "{time.Column}" = CASE
                        WHEN "{time.Column}" LIKE '%+00:00' THEN substr("{time.Column}", 1, length("{time.Column}") - 6)
                        ELSE strftime('%Y-%m-%d %H:%M:%f', "{time.Column}")
                    END
                    WHERE "{time.Column}" GLOB '*[+-][0-9][0-9]:[0-9][0-9]';
                    """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var time in Times)
                migrationBuilder.Sql($"""UPDATE "{time.Table}" SET "{time.Column}" = "{time.Column}" || '+00:00' WHERE "{time.Column}" IS NOT NULL;""");
        }

        private record TimeColumn(string Table, string Column);
    }
}
