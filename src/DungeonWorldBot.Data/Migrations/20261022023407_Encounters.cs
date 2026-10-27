using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Encounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SceneID = table.Column<string>(type: "TEXT", nullable: true),
                    SceneName = table.Column<string>(type: "TEXT", nullable: true),
                    SceneUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AnnouncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndAnnouncedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "MonsterTemplates",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    MaxHP = table.Column<int>(type: "INTEGER", nullable: false),
                    Armor = table.Column<int>(type: "INTEGER", nullable: false),
                    Damage = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Instinct = table.Column<string>(type: "TEXT", nullable: true),
                    Moves = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonsterTemplates", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Enemies",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    EncounterID = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentHP = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxHP = table.Column<int>(type: "INTEGER", nullable: false),
                    Armor = table.Column<int>(type: "INTEGER", nullable: false),
                    Damage = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    Instinct = table.Column<string>(type: "TEXT", nullable: true),
                    Moves = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enemies", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Enemies_Encounters_EncounterID",
                        column: x => x.EncounterID,
                        principalTable: "Encounters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Enemies_EncounterID_Name",
                table: "Enemies",
                columns: new[] { "EncounterID", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonsterTemplates_Name",
                table: "MonsterTemplates",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Enemies");

            migrationBuilder.DropTable(
                name: "MonsterTemplates");

            migrationBuilder.DropTable(
                name: "Encounters");
        }
    }
}
