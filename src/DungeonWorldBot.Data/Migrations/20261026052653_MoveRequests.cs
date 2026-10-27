using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoveRequests",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EncounterID = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    EnemyID = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetEnemyID = table.Column<int>(type: "INTEGER", nullable: true),
                    TargetCharacterID = table.Column<ulong>(type: "INTEGER", nullable: true),
                    TargetNpcID = table.Column<int>(type: "INTEGER", nullable: true),
                    Bonus = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtraDamage = table.Column<int>(type: "INTEGER", nullable: false),
                    Move = table.Column<string>(type: "TEXT", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    ActionID = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoveRequests", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MoveRequests_EncounterActions_ActionID",
                        column: x => x.ActionID,
                        principalTable: "EncounterActions",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MoveRequests_Encounters_EncounterID",
                        column: x => x.EncounterID,
                        principalTable: "Encounters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MoveRequests_Enemies_EnemyID",
                        column: x => x.EnemyID,
                        principalTable: "Enemies",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MoveRequests_Enemies_TargetEnemyID",
                        column: x => x.TargetEnemyID,
                        principalTable: "Enemies",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MoveRequests_Npcs_TargetNpcID",
                        column: x => x.TargetNpcID,
                        principalTable: "Npcs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_ActionID",
                table: "MoveRequests",
                column: "ActionID");

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_EncounterID",
                table: "MoveRequests",
                column: "EncounterID");

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_EnemyID",
                table: "MoveRequests",
                column: "EnemyID");

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_ResolvedAt",
                table: "MoveRequests",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_TargetEnemyID",
                table: "MoveRequests",
                column: "TargetEnemyID");

            migrationBuilder.CreateIndex(
                name: "IX_MoveRequests_TargetNpcID",
                table: "MoveRequests",
                column: "TargetNpcID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MoveRequests");
        }
    }
}
