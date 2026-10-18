using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class NPC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    Role = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentHP = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxHP = table.Column<int>(type: "INTEGER", nullable: false),
                    Armor = table.Column<int>(type: "INTEGER", nullable: false),
                    Damage = table.Column<string>(type: "TEXT", nullable: true),
                    Loyalty = table.Column<int>(type: "INTEGER", nullable: false),
                    Skills = table.Column<string>(type: "TEXT", nullable: true),
                    Cost = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Str = table.Column<int>(type: "INTEGER", nullable: false),
                    Dex = table.Column<int>(type: "INTEGER", nullable: false),
                    Con = table.Column<int>(type: "INTEGER", nullable: false),
                    Int = table.Column<int>(type: "INTEGER", nullable: false),
                    Wis = table.Column<int>(type: "INTEGER", nullable: false),
                    Cha = table.Column<int>(type: "INTEGER", nullable: false),
                    FollowsID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_Name",
                table: "Npcs",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Npcs");
        }
    }
}
