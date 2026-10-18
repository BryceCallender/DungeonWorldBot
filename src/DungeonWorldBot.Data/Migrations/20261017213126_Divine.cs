using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Divine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Move",
                table: "Rolls",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Stat",
                table: "Rolls",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DivineArmor",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MagicArmor",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LevelTaken",
                table: "Ability",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArmorBonus",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmorBonus", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ArmorBonus_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateTable(
                name: "Transformation",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Moves = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    TimesTaken = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transformation", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Transformation_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArmorBonus_CharacterID",
                table: "ArmorBonus",
                column: "CharacterID");

            migrationBuilder.CreateIndex(
                name: "IX_Transformation_CharacterID",
                table: "Transformation",
                column: "CharacterID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArmorBonus");

            migrationBuilder.DropTable(
                name: "Transformation");

            migrationBuilder.DropColumn(
                name: "Move",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "Stat",
                table: "Rolls");

            migrationBuilder.DropColumn(
                name: "DivineArmor",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "MagicArmor",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "LevelTaken",
                table: "Ability");
        }
    }
}
