using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class ItemsRequireInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Item_Inventory_InventoryID",
                table: "Item");

            // Items taken out of an inventory used to be left behind with none. Nobody can see them, and they'd become
            // InventoryID 0, which isn't an inventory.
            migrationBuilder.Sql("DELETE FROM \"Item\" WHERE \"InventoryID\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "InventoryID",
                table: "Item",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Item_Inventory_InventoryID",
                table: "Item",
                column: "InventoryID",
                principalTable: "Inventory",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Item_Inventory_InventoryID",
                table: "Item");

            migrationBuilder.AlterColumn<int>(
                name: "InventoryID",
                table: "Item",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Item_Inventory_InventoryID",
                table: "Item",
                column: "InventoryID",
                principalTable: "Inventory",
                principalColumn: "ID");
        }
    }
}
