using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBeatmapFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beatmap_file");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beatmap_file",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeatmapId = table.Column<int>(type: "INTEGER", nullable: false),
                    BeatmapSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beatmap_file", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_file_BeatmapId",
                table: "beatmap_file",
                column: "BeatmapId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_file_BeatmapSetId",
                table: "beatmap_file",
                column: "BeatmapSetId");
        }
    }
}
