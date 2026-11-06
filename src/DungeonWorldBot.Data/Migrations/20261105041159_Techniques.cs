using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Techniques : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArmorRating",
                table: "Item",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contents",
                table: "Item",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Damage",
                table: "Item",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Item",
                type: "TEXT",
                maxLength: 13,
                nullable: false,
                // Every item so far is a plain one. EF can't load a row whose discriminator it doesn't know, like "".
                defaultValue: "Item");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Item",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Technique",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterID = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSignature = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technique", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Technique_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_SceneID",
                table: "Encounters",
                column: "SceneID",
                unique: true,
                filter: "\"EndedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Technique_CharacterID",
                table: "Technique",
                column: "CharacterID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Technique");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_SceneID",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ArmorRating",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Contents",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Damage",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Item");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Item");
        }
    }
}
