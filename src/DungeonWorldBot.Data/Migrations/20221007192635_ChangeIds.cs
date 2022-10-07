using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class ChangeIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Location_LocationId",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Location",
                newName: "ID");

            migrationBuilder.RenameColumn(
                name: "LocationId",
                table: "Characters",
                newName: "LocationID");

            migrationBuilder.RenameIndex(
                name: "IX_Characters_LocationId",
                table: "Characters",
                newName: "IX_Characters_LocationID");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Bond",
                newName: "ID");

            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Stat",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Location",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Item",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Inventory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Debility",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Location_LocationID",
                table: "Characters",
                column: "LocationID",
                principalTable: "Location",
                principalColumn: "ID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Location_LocationID",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "Location",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "LocationID",
                table: "Characters",
                newName: "LocationId");

            migrationBuilder.RenameIndex(
                name: "IX_Characters_LocationID",
                table: "Characters",
                newName: "IX_Characters_LocationId");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "Bond",
                newName: "Id");

            migrationBuilder.AlterColumn<ulong>(
                name: "ID",
                table: "Stat",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<ulong>(
                name: "Id",
                table: "Location",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<ulong>(
                name: "ID",
                table: "Item",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<ulong>(
                name: "ID",
                table: "Inventory",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<ulong>(
                name: "ID",
                table: "Debility",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Location_LocationId",
                table: "Characters",
                column: "LocationId",
                principalTable: "Location",
                principalColumn: "Id");
        }
    }
}
