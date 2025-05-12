using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserInventoryItemAndBeatmapHypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beatmap_hype",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BeatmapSetId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Hypes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beatmap_hype", x => x.Id);
                    table.ForeignKey(
                        name: "FK_beatmap_hype_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_inventory_item",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_inventory_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_inventory_item_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_hype_BeatmapSetId_Hypes",
                table: "beatmap_hype",
                columns: new[] { "BeatmapSetId", "Hypes" });

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_hype_BeatmapSetId_UserId",
                table: "beatmap_hype",
                columns: new[] { "BeatmapSetId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_hype_UserId",
                table: "beatmap_hype",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_inventory_item_UserId_ItemType",
                table: "user_inventory_item",
                columns: new[] { "UserId", "ItemType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beatmap_hype");

            migrationBuilder.DropTable(
                name: "user_inventory_item");
        }
    }
}
