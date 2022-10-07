using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class Inventory_Character_Updates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Location_LocationId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "Class",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "Modifier",
                table: "Stat",
                newName: "Value");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Characters",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<ulong>(
                name: "LocationId",
                table: "Characters",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "ArmorRating",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<ulong>(
                name: "ClassID",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<ulong>(
                name: "InventoryID",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Class",
                columns: table => new
                {
                    ID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ClassType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Class", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Health",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CurrentHP = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxHP = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Health", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Health_Characters_CharacterID",
                        column: x => x.CharacterID,
                        principalTable: "Characters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Inventory",
                columns: table => new
                {
                    ID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CurrentLoad = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxLoad = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventory", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Item",
                columns: table => new
                {
                    ID = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<int>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    InventoryID = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Item", x => x.ID);
                    table.ForeignKey(
                        name: "FK_Item_Inventory_InventoryID",
                        column: x => x.InventoryID,
                        principalTable: "Inventory",
                        principalColumn: "ID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ClassID",
                table: "Characters",
                column: "ClassID");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_InventoryID",
                table: "Characters",
                column: "InventoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Health_CharacterID",
                table: "Health",
                column: "CharacterID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Item_InventoryID",
                table: "Item",
                column: "InventoryID");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Class_ClassID",
                table: "Characters",
                column: "ClassID",
                principalTable: "Class",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Inventory_InventoryID",
                table: "Characters",
                column: "InventoryID",
                principalTable: "Inventory",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Location_LocationId",
                table: "Characters",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Class_ClassID",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Inventory_InventoryID",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Location_LocationId",
                table: "Characters");

            migrationBuilder.DropTable(
                name: "Class");

            migrationBuilder.DropTable(
                name: "Health");

            migrationBuilder.DropTable(
                name: "Item");

            migrationBuilder.DropTable(
                name: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Characters_ClassID",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_InventoryID",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ArmorRating",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ClassID",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "InventoryID",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "Stat",
                newName: "Modifier");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<ulong>(
                name: "LocationId",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Class",
                table: "Characters",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Location_LocationId",
                table: "Characters",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
