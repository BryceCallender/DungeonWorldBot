using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class Rename_Property : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClassType",
                table: "Class",
                newName: "Type");

            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Class",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Class",
                newName: "ClassType");

            migrationBuilder.AlterColumn<ulong>(
                name: "ID",
                table: "Class",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);
        }
    }
}
