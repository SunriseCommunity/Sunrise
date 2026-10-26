using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalRecalculationLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RunId",
                table: "score_processing_task",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalculationVersionId",
                table: "score",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "beatmap_hash_status",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BeatmapHash = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeatmapId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MissCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beatmap_hash_status", x => x.Id);
                    table.UniqueConstraint("AK_beatmap_hash_status_BeatmapHash", x => x.BeatmapHash);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "calculation_version",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RosuVersion = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SunriseRevision = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calculation_version", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "calculation_run",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TargetVersionId = table.Column<int>(type: "int", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsSuperseded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsForced = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calculation_run", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calculation_run_calculation_version_TargetVersionId",
                        column: x => x.TargetVersionId,
                        principalTable: "calculation_version",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_RunId_Status",
                table: "score_processing_task",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_score_CalculationVersionId",
                table: "score",
                column: "CalculationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_score_GameMode_SubmissionStatus_WhenPlayed",
                table: "score",
                columns: new[] { "GameMode", "SubmissionStatus", "WhenPlayed" });

            migrationBuilder.CreateIndex(
                name: "IX_score_UserId_SubmissionStatus",
                table: "score",
                columns: new[] { "UserId", "SubmissionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_beatmap_hash_status_CheckedAt",
                table: "beatmap_hash_status",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_calculation_run_FinishedAt",
                table: "calculation_run",
                column: "FinishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_calculation_run_TargetVersionId",
                table: "calculation_run",
                column: "TargetVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_calculation_version_RosuVersion_SunriseRevision",
                table: "calculation_version",
                columns: new[] { "RosuVersion", "SunriseRevision" },
                unique: true);

            migrationBuilder.Sql("INSERT INTO calculation_version (Id, RosuVersion, SunriseRevision) VALUES (1, '3.1.0', 1);");
            migrationBuilder.Sql("UPDATE score SET CalculationVersionId = 1;");
            migrationBuilder.Sql("INSERT INTO calculation_run (TargetVersionId, Phase, StartedAt, FinishedAt, IsSuperseded, IsForced) VALUES (1, 4, UTC_TIMESTAMP(), UTC_TIMESTAMP(), 0, 0);");
            migrationBuilder.Sql(@"
                INSERT INTO beatmap_hash_status (BeatmapHash, BeatmapId, Status, CheckedAt, MissCount)
                SELECT s.BeatmapHash, s.BeatmapId, s.BeatmapStatus, '2000-01-01', 0
                FROM score s
                JOIN (SELECT MAX(Id) AS Id FROM score GROUP BY BeatmapHash) latest ON latest.Id = s.Id;");

            migrationBuilder.DropIndex(
                name: "IX_score_GameMode_SubmissionStatus_BeatmapStatus_WhenPlayed",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_UserId_SubmissionStatus_BeatmapStatus",
                table: "score");

            migrationBuilder.DropColumn(
                name: "BeatmapStatus",
                table: "score");

            migrationBuilder.AddForeignKey(
                name: "FK_score_beatmap_hash_status_BeatmapHash",
                table: "score",
                column: "BeatmapHash",
                principalTable: "beatmap_hash_status",
                principalColumn: "BeatmapHash",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_score_calculation_version_CalculationVersionId",
                table: "score",
                column: "CalculationVersionId",
                principalTable: "calculation_version",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_score_processing_task_calculation_run_RunId",
                table: "score_processing_task",
                column: "RunId",
                principalTable: "calculation_run",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_score_beatmap_hash_status_BeatmapHash",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_score_calculation_version_CalculationVersionId",
                table: "score");

            migrationBuilder.DropForeignKey(
                name: "FK_score_processing_task_calculation_run_RunId",
                table: "score_processing_task");

            migrationBuilder.AddColumn<int>(
                name: "BeatmapStatus",
                table: "score",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE score s JOIN beatmap_hash_status h ON h.BeatmapHash = s.BeatmapHash SET s.BeatmapStatus = h.Status;");

            migrationBuilder.DropTable(
                name: "beatmap_hash_status");

            migrationBuilder.DropTable(
                name: "calculation_run");

            migrationBuilder.DropTable(
                name: "calculation_version");

            migrationBuilder.DropIndex(
                name: "IX_score_processing_task_RunId_Status",
                table: "score_processing_task");

            migrationBuilder.DropIndex(
                name: "IX_score_CalculationVersionId",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_GameMode_SubmissionStatus_WhenPlayed",
                table: "score");

            migrationBuilder.DropIndex(
                name: "IX_score_UserId_SubmissionStatus",
                table: "score");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "score_processing_task");

            migrationBuilder.DropColumn(
                name: "CalculationVersionId",
                table: "score");

            migrationBuilder.CreateIndex(
                name: "IX_score_GameMode_SubmissionStatus_BeatmapStatus_WhenPlayed",
                table: "score",
                columns: new[] { "GameMode", "SubmissionStatus", "BeatmapStatus", "WhenPlayed" });

            migrationBuilder.CreateIndex(
                name: "IX_score_UserId_SubmissionStatus_BeatmapStatus",
                table: "score",
                columns: new[] { "UserId", "SubmissionStatus", "BeatmapStatus" });
        }
    }
}
