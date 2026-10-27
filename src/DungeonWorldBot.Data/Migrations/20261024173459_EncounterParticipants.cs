using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncounterParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncounterParticipants",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EncounterID = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    NpcID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipants", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_Encounters_EncounterID",
                        column: x => x.EncounterID,
                        principalTable: "Encounters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_Npcs_NpcID",
                        column: x => x.NpcID,
                        principalTable: "Npcs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_EncounterID",
                table: "EncounterParticipants",
                column: "EncounterID");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_NpcID",
                table: "EncounterParticipants",
                column: "NpcID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterParticipants");
        }
    }
}
