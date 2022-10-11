using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class Bond_Updates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ID",
                table: "Bond",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddColumn<ulong>(
                name: "TargetID",
                table: "Bond",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetID",
                table: "Bond");

            migrationBuilder.AlterColumn<ulong>(
                name: "ID",
                table: "Bond",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);
        }
    }
}
