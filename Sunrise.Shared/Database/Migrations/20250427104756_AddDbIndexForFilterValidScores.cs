using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddDbIndexForFilterValidScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_score_UserId_SubmissionStatus_BeatmapStatus",
                table: "score",
                columns: new[] { "UserId", "SubmissionStatus", "BeatmapStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_score_UserId_SubmissionStatus_BeatmapStatus",
                table: "score");
        }
    }
}
