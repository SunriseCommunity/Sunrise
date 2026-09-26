using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBeatmapHashGameModeIndexToScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_score_UserId_BeatmapHash_GameMode",
                table: "score",
                columns: new[] { "UserId", "BeatmapHash", "GameMode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_score_UserId_BeatmapHash_GameMode",
                table: "score");
        }
    }
}
