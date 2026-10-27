using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class EncounterActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncounterActions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EncounterID = table.Column<int>(type: "INTEGER", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    EnemyID = table.Column<int>(type: "INTEGER", nullable: true),
                    NpcID = table.Column<int>(type: "INTEGER", nullable: true),
                    MessageID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Roll = table.Column<string>(type: "TEXT", nullable: true),
                    Target = table.Column<string>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterActions", x => x.ID);
                    table.ForeignKey(
                        name: "FK_EncounterActions_Encounters_EncounterID",
                        column: x => x.EncounterID,
                        principalTable: "Encounters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncounterActions_Enemies_EnemyID",
                        column: x => x.EnemyID,
                        principalTable: "Enemies",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EncounterActions_Npcs_NpcID",
                        column: x => x.NpcID,
                        principalTable: "Npcs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterActions_EncounterID",
                table: "EncounterActions",
                column: "EncounterID");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterActions_EnemyID",
                table: "EncounterActions",
                column: "EnemyID");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterActions_NpcID",
                table: "EncounterActions",
                column: "NpcID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterActions");
        }
    }
}
