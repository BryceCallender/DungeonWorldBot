using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class Navigation_Prop_Again : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stat_Characters_CharacterID",
                table: "Stat");

            migrationBuilder.AlterColumn<ulong>(
                name: "CharacterID",
                table: "Stat",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Stat_Characters_CharacterID",
                table: "Stat",
                column: "CharacterID",
                principalTable: "Characters",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stat_Characters_CharacterID",
                table: "Stat");

            migrationBuilder.AlterColumn<ulong>(
                name: "CharacterID",
                table: "Stat",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Stat_Characters_CharacterID",
                table: "Stat",
                column: "CharacterID",
                principalTable: "Characters",
                principalColumn: "ID");
        }
    }
}
