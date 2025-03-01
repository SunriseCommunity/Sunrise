using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class EnrichDatabaseWithEF : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "migration");

            migrationBuilder.DropPrimaryKey(
                name: "PK_score",
                table: "score");

            migrationBuilder.AlterColumn<string>(
                name: "SnapshotsJson",
                table: "user_stats_snapshot",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "user_file",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Passhash",
                table: "user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Friends",
                table: "user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(1024)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "user",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldNullable: true,
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "ScoreHash",
                table: "score",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<double>(
                name: "PerformancePoints",
                table: "score",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "DECIMAL(100, 2)");

            migrationBuilder.AlterColumn<bool>(
                name: "Perfect",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "TINYINT");

            migrationBuilder.AlterColumn<string>(
                name: "OsuVersion",
                table: "score",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<bool>(
                name: "IsScoreable",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "TINYINT");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPassed",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "TINYINT");

            migrationBuilder.AlterColumn<string>(
                name: "Grade",
                table: "score",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "BeatmapHash",
                table: "score",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<double>(
                name: "Accuracy",
                table: "score",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "DECIMAL(100, 2)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .OldAnnotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "restriction",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "medal_file",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(1024)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "medal",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(1024)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "medal",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "VARCHAR(1024)",
                oldNullable: true,
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "medal",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(1024)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Condition",
                table: "medal",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(1024)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "JsonData",
                table: "event_user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Ip",
                table: "event_user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(64)",
                oldCollation: "NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "beatmap_file",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "VARCHAR(2147483647)",
                oldCollation: "NOCASE");

            migrationBuilder.AddPrimaryKey(
                name: "PK_score",
                table: "score",
                columns: new[] { "Id", "ScoreHash" });

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_snapshot_UserId_GameMode",
                table: "user_stats_snapshot",
                columns: new[] { "UserId", "GameMode" });

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_GameMode",
                table: "user_stats",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_UserId",
                table: "user_stats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_medals_UserId",
                table: "user_medals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_file_OwnerId",
                table: "user_file",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_user_file_Type",
                table: "user_file",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_user_favourite_beatmap_BeatmapSetId",
                table: "user_favourite_beatmap",
                column: "BeatmapSetId");

            migrationBuilder.CreateIndex(
                name: "IX_user_favourite_beatmap_UserId",
                table: "user_favourite_beatmap",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_AccountStatus",
                table: "user",
                column: "AccountStatus");

            migrationBuilder.CreateIndex(
                name: "IX_user_Email",
                table: "user",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_Username",
                table: "user",
                column: "Username",
                unique: true);

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
                name: "IX_score_ReplayFileId",
                table: "score",
                column: "ReplayFileId");

            migrationBuilder.CreateIndex(
                name: "IX_score_SubmissionStatus",
                table: "score",
                column: "SubmissionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_score_TotalScore",
                table: "score",
                column: "TotalScore");

            migrationBuilder.CreateIndex(
                name: "IX_score_UserId",
                table: "score",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_restriction_UserId",
                table: "restriction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_medal_file_MedalId",
                table: "medal_file",
                column: "MedalId");

            migrationBuilder.CreateIndex(
                name: "IX_medal_Category",
                table: "medal",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_medal_FileId",
                table: "medal",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_medal_GameMode",
                table: "medal",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_event_user_EventType_UserId",
                table: "event_user",
                columns: new[] { "EventType", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_event_user_Ip",
                table: "event_user",
                column: "Ip");

            migrationBuilder.CreateIndex(
                name: "IX_event_user_UserId",
                table: "event_user",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_file_BeatmapId",
                table: "beatmap_file",
                column: "BeatmapId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_file_BeatmapSetId",
                table: "beatmap_file",
                column: "BeatmapSetId");

            migrationBuilder.AddForeignKey(
                name: "FK_event_user_user_UserId",
                table: "event_user",
                column: "UserId",
                principalTable: "user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_medal_medal_file_FileId",
                table: "medal",
                column: "FileId",
                principalTable: "medal_file",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_medal_file_medal_MedalId",
                table: "medal_file",
                column: "MedalId",
                principalTable: "medal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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
                name: "FK_score_user_file_ReplayFileId",
                table: "score",
                column: "ReplayFileId",
                principalTable: "user_file",
                principalColumn: "Id");

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
                name: "FK_medal_medal_file_FileId",
                table: "medal");

            migrationBuilder.DropForeignKey(
                name: "FK_medal_file_medal_MedalId",
                table: "medal_file");

            migrationBuilder.DropForeignKey(
                name: "FK_restriction_user_UserId",
                table: "restriction");

            migrationBuilder.DropForeignKey(
                name: "FK_score_user_UserId",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_score_user_file_ReplayFileId",
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

            migrationBuilder.DropIndex(
                name: "IX_user_stats_snapshot_UserId_GameMode",
                table: "user_stats_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_user_stats_GameMode",
                table: "user_stats");

            migrationBuilder.DropIndex(
                name: "IX_user_stats_UserId",
                table: "user_stats");

            migrationBuilder.DropIndex(
                name: "IX_user_medals_UserId",
                table: "user_medals");

            migrationBuilder.DropIndex(
                name: "IX_user_file_OwnerId",
                table: "user_file");

            migrationBuilder.DropIndex(
                name: "IX_user_file_Type",
                table: "user_file");

            migrationBuilder.DropIndex(
                name: "IX_user_favourite_beatmap_BeatmapSetId",
                table: "user_favourite_beatmap");

            migrationBuilder.DropIndex(
                name: "IX_user_favourite_beatmap_UserId",
                table: "user_favourite_beatmap");

            migrationBuilder.DropIndex(
                name: "IX_user_AccountStatus",
                table: "user");

            migrationBuilder.DropIndex(
                name: "IX_user_Email",
                table: "user");

            migrationBuilder.DropIndex(
                name: "IX_user_Username",
                table: "user");

            migrationBuilder.DropPrimaryKey(
                name: "PK_score",
                table: "score");

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
                name: "IX_score_ReplayFileId",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_SubmissionStatus",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_TotalScore",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_UserId",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_restriction_UserId",
                table: "restriction");

            migrationBuilder.DropIndex(
                name: "IX_medal_file_MedalId",
                table: "medal_file");

            migrationBuilder.DropIndex(
                name: "IX_medal_Category",
                table: "medal");

            migrationBuilder.DropIndex(
                name: "IX_medal_FileId",
                table: "medal");

            migrationBuilder.DropIndex(
                name: "IX_medal_GameMode",
                table: "medal");

            migrationBuilder.DropIndex(
                name: "IX_event_user_EventType_UserId",
                table: "event_user");

            migrationBuilder.DropIndex(
                name: "IX_event_user_Ip",
                table: "event_user");

            migrationBuilder.DropIndex(
                name: "IX_event_user_UserId",
                table: "event_user");

            migrationBuilder.DropIndex(
                name: "IX_beatmap_file_BeatmapId",
                table: "beatmap_file");

            migrationBuilder.DropIndex(
                name: "IX_beatmap_file_BeatmapSetId",
                table: "beatmap_file");

            migrationBuilder.AlterColumn<string>(
                name: "SnapshotsJson",
                table: "user_stats_snapshot",
                type: "VARCHAR(2147483647)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "user_file",
                type: "VARCHAR(2147483647)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "user",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Passhash",
                table: "user",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Friends",
                table: "user",
                type: "VARCHAR(2147483647)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "user",
                type: "VARCHAR(1024)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "user",
                type: "VARCHAR(2147483647)",
                nullable: true,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "PerformancePoints",
                table: "score",
                type: "DECIMAL(100, 2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<byte>(
                name: "Perfect",
                table: "score",
                type: "TINYINT",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "OsuVersion",
                table: "score",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<byte>(
                name: "IsScoreable",
                table: "score",
                type: "TINYINT",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<byte>(
                name: "IsPassed",
                table: "score",
                type: "TINYINT",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Grade",
                table: "score",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "BeatmapHash",
                table: "score",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Accuracy",
                table: "score",
                type: "DECIMAL(100, 2)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL");

            migrationBuilder.AlterColumn<string>(
                name: "ScoreHash",
                table: "score",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "score",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Sqlite:Autoincrement", true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "restriction",
                type: "VARCHAR(2147483647)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "medal_file",
                type: "VARCHAR(1024)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "medal",
                type: "VARCHAR(1024)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "medal",
                type: "VARCHAR(1024)",
                nullable: true,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "medal",
                type: "VARCHAR(1024)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Condition",
                table: "medal",
                type: "VARCHAR(1024)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "JsonData",
                table: "event_user",
                type: "VARCHAR(2147483647)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Ip",
                table: "event_user",
                type: "VARCHAR(64)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "beatmap_file",
                type: "VARCHAR(2147483647)",
                nullable: false,
                collation: "NOCASE",
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddPrimaryKey(
                name: "PK_score",
                table: "score",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migration", x => x.Id);
                });
        }
    }
}
