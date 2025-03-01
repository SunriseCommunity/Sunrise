using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixScoreIdIsNotAutoInrecement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_user_Users_UserId",
                table: "event_user");

            migrationBuilder.DropForeignKey(
                name: "FK_restriction_Users_ExecutorId",
                table: "restriction");

            migrationBuilder.DropForeignKey(
                name: "FK_restriction_Users_UserId",
                table: "restriction");

            migrationBuilder.DropForeignKey(
                name: "FK_score_Users_UserId",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_beatmap_Users_UserId",
                table: "user_favourite_beatmap");

            migrationBuilder.DropForeignKey(
                name: "FK_user_file_Users_OwnerId",
                table: "user_file");

            migrationBuilder.DropForeignKey(
                name: "FK_user_medals_Users_UserId",
                table: "user_medals");

            migrationBuilder.DropForeignKey(
                name: "FK_user_stats_Users_UserId",
                table: "user_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_user_stats_snapshot_Users_UserId",
                table: "user_stats_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "PK_score",
                table: "score");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "user");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Username",
                table: "user",
                newName: "IX_user_Username");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                table: "user",
                newName: "IX_user_Email");

            migrationBuilder.RenameIndex(
                name: "IX_Users_AccountStatus",
                table: "user",
                newName: "IX_user_AccountStatus");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_score",
                table: "score",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_user",
                table: "user",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_event_user_user_UserId",
                table: "event_user",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_restriction_user_ExecutorId",
                table: "restriction",
                column: "ExecutorId",
                principalTable: "user",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_restriction_user_UserId",
                table: "restriction",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_score_user_UserId",
                table: "score",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_beatmap_user_UserId",
                table: "user_favourite_beatmap",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_file_user_OwnerId",
                table: "user_file",
                column: "OwnerId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_medals_user_UserId",
                table: "user_medals",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_stats_user_UserId",
                table: "user_stats",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_stats_snapshot_user_UserId",
                table: "user_stats_snapshot",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_user_user_UserId",
                table: "event_user");

            migrationBuilder.DropForeignKey(
                name: "FK_restriction_user_ExecutorId",
                table: "restriction");

            migrationBuilder.DropForeignKey(
                name: "FK_restriction_user_UserId",
                table: "restriction");

            migrationBuilder.DropForeignKey(
                name: "FK_score_user_UserId",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_user_favourite_beatmap_user_UserId",
                table: "user_favourite_beatmap");

            migrationBuilder.DropForeignKey(
                name: "FK_user_file_user_OwnerId",
                table: "user_file");

            migrationBuilder.DropForeignKey(
                name: "FK_user_medals_user_UserId",
                table: "user_medals");

            migrationBuilder.DropForeignKey(
                name: "FK_user_stats_user_UserId",
                table: "user_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_user_stats_snapshot_user_UserId",
                table: "user_stats_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "PK_score",
                table: "score");

            migrationBuilder.DropPrimaryKey(
                name: "PK_user",
                table: "user");

            migrationBuilder.RenameTable(
                name: "user",
                newName: "Users");

            migrationBuilder.RenameIndex(
                name: "IX_user_Username",
                table: "Users",
                newName: "IX_Users_Username");

            migrationBuilder.RenameIndex(
                name: "IX_user_Email",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameIndex(
                name: "IX_user_AccountStatus",
                table: "Users",
                newName: "IX_Users_AccountStatus");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_score",
                table: "score",
                columns: new[] { "Id", "ScoreHash" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_event_user_Users_UserId",
                table: "event_user",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_restriction_Users_ExecutorId",
                table: "restriction",
                column: "ExecutorId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_restriction_Users_UserId",
                table: "restriction",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_score_Users_UserId",
                table: "score",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_favourite_beatmap_Users_UserId",
                table: "user_favourite_beatmap",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_file_Users_OwnerId",
                table: "user_file",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_medals_Users_UserId",
                table: "user_medals",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_stats_Users_UserId",
                table: "user_stats",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_stats_snapshot_Users_UserId",
                table: "user_stats_snapshot",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
