using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class StoryAndNotesAndGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Backstory",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Goal",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goal", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Goal_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Note",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Note", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Note_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Goal_CharacterID",
                table: "Goal",
                column: "CharacterID");

            migrationBuilder.CreateIndex(
                name: "IX_Note_CharacterID",
                table: "Note",
                column: "CharacterID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Goal");

            migrationBuilder.DropTable(
                name: "Note");

            migrationBuilder.DropColumn(
                name: "Backstory",
                table: "Characters");
        }
    }
}
