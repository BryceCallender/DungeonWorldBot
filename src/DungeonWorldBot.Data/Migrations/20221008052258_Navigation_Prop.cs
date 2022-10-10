using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Data.Migrations
{
    public partial class Navigation_Prop : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Class_ClassID",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_ClassID",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ClassID",
                table: "Characters");

            migrationBuilder.AddColumn<ulong>(
                name: "CharacterID",
                table: "Class",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.CreateIndex(
                name: "IX_Class_CharacterID",
                table: "Class",
                column: "CharacterID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Class_Characters_CharacterID",
                table: "Class",
                column: "CharacterID",
                principalTable: "Characters",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Class_Characters_CharacterID",
                table: "Class");

            migrationBuilder.DropIndex(
                name: "IX_Class_CharacterID",
                table: "Class");

            migrationBuilder.DropColumn(
                name: "CharacterID",
                table: "Class");

            migrationBuilder.AddColumn<int>(
                name: "ClassID",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Characters_ClassID",
                table: "Characters",
                column: "ClassID");

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Class_ClassID",
                table: "Characters",
                column: "ClassID",
                principalTable: "Class",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
