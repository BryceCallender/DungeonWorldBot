using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    ID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Class = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Alignment = table.Column<int>(type: "INTEGER", nullable: false),
                    Race = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Bond",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bond", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bond_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Debility",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StatType = table.Column<int>(type: "INTEGER", nullable: false),
                    Modifier = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Debility", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Debility_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Stat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StatType = table.Column<int>(type: "INTEGER", nullable: false),
                    Modifier = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stat", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stat_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bond_CharacterID",
                table: "Bond",
                column: "CharacterID");

            migrationBuilder.CreateIndex(
                name: "IX_Debility_CharacterID",
                table: "Debility",
                column: "CharacterID");

            migrationBuilder.CreateIndex(
                name: "IX_Stat_CharacterID",
                table: "Stat",
                column: "CharacterID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bond");

            migrationBuilder.DropTable(
                name: "Debility");

            migrationBuilder.DropTable(
                name: "Stat");

            migrationBuilder.DropTable(
                name: "Characters");
        }
    }
}
