using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class Rolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rolls",
                columns: table => new
                {
                    MessageID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ChannelID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UserID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    First = table.Column<int>(type: "INTEGER", nullable: false),
                    Second = table.Column<int>(type: "INTEGER", nullable: false),
                    Total = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rolls", x => x.MessageID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rolls_UserID",
                table: "Rolls",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rolls");
        }
    }
}
