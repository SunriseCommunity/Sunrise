using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeIndexesUsages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_stats_GameMode",
                table: "user_stats");

            migrationBuilder.DropIndex(
                name: "IX_user_file_Type",
                table: "user_file");

            migrationBuilder.DropIndex(
                name: "IX_user_favourite_beatmap_BeatmapSetId",
                table: "user_favourite_beatmap");

            migrationBuilder.DropIndex(
                name: "IX_score_BeatmapId",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_BeatmapStatus",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_GameMode",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_Mods",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_PerformancePoints",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_SubmissionStatus",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_TotalScore",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_event_user_Ip",
                table: "event_user");

            migrationBuilder.AlterColumn<string>(
                name: "BeatmapHash",
                table: "score",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.AlterColumn<string>(
                name: "BeatmapHash",
                table: "custom_beatmap_status",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_UserId_GameMode",
                table: "user_stats",
                columns: new[] { "UserId", "GameMode" });

            migrationBuilder.CreateIndex(
                name: "IX_user_file_OwnerId_Type",
                table: "user_file",
                columns: new[] { "OwnerId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_user_favourite_beatmap_UserId_BeatmapSetId",
                table: "user_favourite_beatmap",
                columns: new[] { "UserId", "BeatmapSetId" });

            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapHash",
                table: "score",
                column: "BeatmapHash");

            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapId_IsScoreable_IsPassed_SubmissionStatus",
                table: "score",
                columns: new[] { "BeatmapId", "IsScoreable", "IsPassed", "SubmissionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_score_GameMode_SubmissionStatus_BeatmapStatus_WhenPlayed",
                table: "score",
                columns: new[] { "GameMode", "SubmissionStatus", "BeatmapStatus", "WhenPlayed" });

            migrationBuilder.CreateIndex(
                name: "IX_score_UserId_BeatmapId",
                table: "score",
                columns: new[] { "UserId", "BeatmapId" });

            migrationBuilder.CreateIndex(
                name: "IX_event_user_EventType_Ip",
                table: "event_user",
                columns: new[] { "EventType", "Ip" });

            migrationBuilder.CreateIndex(
                name: "IX_custom_beatmap_status_BeatmapHash",
                table: "custom_beatmap_status",
                column: "BeatmapHash");

            migrationBuilder.CreateIndex(
                name: "IX_custom_beatmap_status_BeatmapSetId",
                table: "custom_beatmap_status",
                column: "BeatmapSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_stats_UserId_GameMode",
                table: "user_stats");

            migrationBuilder.DropIndex(
                name: "IX_user_file_OwnerId_Type",
                table: "user_file");

            migrationBuilder.DropIndex(
                name: "IX_user_favourite_beatmap_UserId_BeatmapSetId",
                table: "user_favourite_beatmap");

            migrationBuilder.DropIndex(
                name: "IX_score_BeatmapHash",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_BeatmapId_IsScoreable_IsPassed_SubmissionStatus",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_GameMode_SubmissionStatus_BeatmapStatus_WhenPlayed",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_UserId_BeatmapId",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_event_user_EventType_Ip",
                table: "event_user");

            migrationBuilder.DropIndex(
                name: "IX_custom_beatmap_status_BeatmapHash",
                table: "custom_beatmap_status");

            migrationBuilder.DropIndex(
                name: "IX_custom_beatmap_status_BeatmapSetId",
                table: "custom_beatmap_status");

            migrationBuilder.AlterColumn<string>(
                name: "BeatmapHash",
                table: "score",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<string>(
                name: "BeatmapHash",
                table: "custom_beatmap_status",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_GameMode",
                table: "user_stats",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_user_file_Type",
                table: "user_file",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_user_favourite_beatmap_BeatmapSetId",
                table: "user_favourite_beatmap",
                column: "BeatmapSetId");

            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapId",
                table: "score",
                column: "BeatmapId");

            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapStatus",
                table: "score",
                column: "BeatmapStatus");

            migrationBuilder.CreateIndex(
                name: "IX_score_GameMode",
                table: "score",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_score_Mods",
                table: "score",
                column: "Mods");

            migrationBuilder.CreateIndex(
                name: "IX_score_PerformancePoints",
                table: "score",
                column: "PerformancePoints");

            migrationBuilder.CreateIndex(
                name: "IX_score_SubmissionStatus",
                table: "score",
                column: "SubmissionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_score_TotalScore",
                table: "score",
                column: "TotalScore");

            migrationBuilder.CreateIndex(
                name: "IX_event_user_Ip",
                table: "event_user",
                column: "Ip");
        }
    }
}
