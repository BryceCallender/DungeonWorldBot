using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DungeonWorldBot.Dashboard.Canvases.Migrations
{
    /// <inheritdoc />
    public partial class InitialCanvases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Canvases",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Canvases", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CanvasImages",
                columns: table => new
                {
                    ID = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CanvasID = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UploadedBy = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasImages", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CanvasImages_Canvases_CanvasID",
                        column: x => x.CanvasID,
                        principalTable: "Canvases",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanvasItems",
                columns: table => new
                {
                    ID = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CanvasID = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorID = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasItems", x => new { x.CanvasID, x.ID });
                    table.ForeignKey(
                        name: "FK_CanvasItems_Canvases_CanvasID",
                        column: x => x.CanvasID,
                        principalTable: "Canvases",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CanvasThumbnails",
                columns: table => new
                {
                    CanvasID = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasThumbnails", x => x.CanvasID);
                    table.ForeignKey(
                        name: "FK_CanvasThumbnails_Canvases_CanvasID",
                        column: x => x.CanvasID,
                        principalTable: "Canvases",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Canvases_UpdatedAt",
                table: "Canvases",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasImages_CanvasID",
                table: "CanvasImages",
                column: "CanvasID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasImages");

            migrationBuilder.DropTable(
                name: "CanvasItems");

            migrationBuilder.DropTable(
                name: "CanvasThumbnails");

            migrationBuilder.DropTable(
                name: "Canvases");
        }
    }
}
