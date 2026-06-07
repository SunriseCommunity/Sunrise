using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreSubmissionRequestAndProcessingTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ScoreHash",
                table: "score",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext");

            migrationBuilder.CreateTable(
                name: "score_submission_request",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ScoreHash = table.Column<string>(type: "varchar(255)", nullable: false),
                    ScoreSerialized = table.Column<string>(type: "longtext", nullable: false),
                    BeatmapHash = table.Column<string>(type: "longtext", nullable: false),
                    TimeElapsed = table.Column<int>(type: "int", nullable: false),
                    OsuVersion = table.Column<string>(type: "longtext", nullable: false),
                    ClientHash = table.Column<string>(type: "longtext", nullable: false),
                    ReplayFileId = table.Column<int>(type: "int", nullable: true),
                    StoryboardHash = table.Column<string>(type: "longtext", nullable: true),
                    UserHash = table.Column<string>(type: "longtext", nullable: false),
                    WhenPlayed = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_submission_request", x => x.Id);
                    table.ForeignKey(
                        name: "FK_score_submission_request_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_score_submission_request_user_file_ReplayFileId",
                        column: x => x.ReplayFileId,
                        principalTable: "user_file",
                        principalColumn: "Id");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "score_processing_task",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    ScoreSubmissionRequestId = table.Column<int>(type: "int", nullable: true),
                    ScoreId = table.Column<int>(type: "int", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: true),
                    ClaimToken = table.Column<string>(type: "longtext", nullable: true),
                    LeaseExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActiveScoreId = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN Status IN (0, 1) THEN ScoreId ELSE NULL END", stored: true),
                    ActiveScoreSubmissionRequestId = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN Status IN (0, 1) THEN ScoreSubmissionRequestId ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_processing_task", x => x.Id);
                    table.CheckConstraint("CK_score_processing_task_target", "((TaskType = 0 AND ScoreSubmissionRequestId IS NOT NULL AND ScoreId IS NULL) OR (TaskType <> 0 AND ScoreSubmissionRequestId IS NULL AND ScoreId IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_score_processing_task_score_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "score",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_score_processing_task_score_submission_request_ScoreSubmissi~",
                        column: x => x.ScoreSubmissionRequestId,
                        principalTable: "score_submission_request",
                        principalColumn: "Id");
                })
                .Annotation("MySQL:Charset", "utf8mb4");
            
            migrationBuilder.CreateIndex(
                name: "IX_score_ScoreHash_Status_Id",
                table: "score",
                columns: new[] { "ScoreHash", "SubmissionStatus", "Id" });
   
            migrationBuilder.Sql(@"
                DELETE FROM score
                WHERE Id IN (
                    SELECT Id FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY ScoreHash
                                   ORDER BY SubmissionStatus DESC, Id DESC
                               ) AS rn
                        FROM score
                    ) t
                    WHERE t.rn > 1
                );
                ");
            
            migrationBuilder.DropIndex(
                name: "IX_score_ScoreHash_Status_Id",
                table: "score");
            
            migrationBuilder.CreateIndex(
                name: "IX_score_ScoreHash",
                table: "score",
                column: "ScoreHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_score_submission_request_ReplayFileId",
                table: "score_submission_request",
                column: "ReplayFileId");

            migrationBuilder.CreateIndex(
                name: "IX_score_submission_request_ScoreHash",
                table: "score_submission_request",
                column: "ScoreHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_score_submission_request_UserId",
                table: "score_submission_request",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_ScoreId",
                table: "score_processing_task",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_ScoreSubmissionRequestId",
                table: "score_processing_task",
                column: "ScoreSubmissionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_Status_LeaseExpiresAt",
                table: "score_processing_task",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_Status_Priority_NextRetryAt",
                table: "score_processing_task",
                columns: new[] { "Status", "Priority", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_TaskType_ScoreId",
                table: "score_processing_task",
                columns: new[] { "TaskType", "ScoreId" });

            migrationBuilder.CreateIndex(
                name: "UX_score_processing_task_active_submission_request",
                table: "score_processing_task",
                column: "ActiveScoreSubmissionRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_score_processing_task_active_score",
                table: "score_processing_task",
                column: "ActiveScoreId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_processing_task");

            migrationBuilder.DropTable(
                name: "score_submission_request");

            migrationBuilder.DropIndex(
                name: "IX_score_ScoreHash",
                table: "score");

            migrationBuilder.AlterColumn<string>(
                name: "ScoreHash",
                table: "score",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");
        }
    }
}


