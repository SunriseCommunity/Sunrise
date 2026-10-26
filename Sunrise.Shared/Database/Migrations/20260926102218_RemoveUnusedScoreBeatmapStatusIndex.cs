using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedScoreBeatmapStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_score_BeatmapId_IsScoreable_IsPassed_SubmissionStatus",
                table: "score");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapId_IsScoreable_IsPassed_SubmissionStatus",
                table: "score",
                columns: new[] { "BeatmapId", "IsScoreable", "IsPassed", "SubmissionStatus" });
        }
    }
}
