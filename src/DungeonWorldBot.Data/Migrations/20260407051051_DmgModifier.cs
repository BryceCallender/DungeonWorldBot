using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class DmgModifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Inventory_InventoryID",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_InventoryID",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "InventoryID",
                table: "Characters");

            migrationBuilder.AddColumn<ulong>(
                name: "CharacterID",
                table: "Inventory",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<string>(
                name: "DamageModifier",
                table: "Characters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_CharacterID",
                table: "Inventory",
                column: "CharacterID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Characters_CharacterID",
                table: "Inventory",
                column: "CharacterID",
                principalTable: "Characters",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Characters_CharacterID",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_CharacterID",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "CharacterID",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "DamageModifier",
                table: "Characters");

            migrationBuilder.AddColumn<int>(
                name: "InventoryID",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_InventoryID",
                table: "Characters",
                column: "InventoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Inventory_InventoryID",
                table: "Characters",
                column: "InventoryID",
                principalTable: "Inventory",
                principalColumn: "ID");
        }
    }
}
