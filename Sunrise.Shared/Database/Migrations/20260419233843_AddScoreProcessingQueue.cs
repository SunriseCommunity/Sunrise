using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreProcessingQueue : Migration
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
                name: "score_processing_queue",
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
                    table.PrimaryKey("PK_score_processing_queue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_score_processing_queue_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_score_processing_queue_user_file_ReplayFileId",
                        column: x => x.ReplayFileId,
                        principalTable: "user_file",
                        principalColumn: "Id");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "score_task_queue",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    ScoreProcessingQueueId = table.Column<int>(type: "int", nullable: true),
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
                    ActiveScoreProcessingQueueId = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN Status IN (0, 1) THEN ScoreProcessingQueueId ELSE NULL END", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_task_queue", x => x.Id);
                    table.CheckConstraint("CK_score_task_queue_target", "((TaskType = 0 AND ScoreProcessingQueueId IS NOT NULL AND ScoreId IS NULL) OR (TaskType <> 0 AND ScoreProcessingQueueId IS NULL AND ScoreId IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_score_task_queue_score_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "score",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_score_task_queue_score_processing_queue_ScoreProcessingQueue~",
                        column: x => x.ScoreProcessingQueueId,
                        principalTable: "score_processing_queue",
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
                name: "IX_score_processing_queue_ReplayFileId",
                table: "score_processing_queue",
                column: "ReplayFileId");

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_queue_ScoreHash",
                table: "score_processing_queue",
                column: "ScoreHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_score_processing_queue_UserId",
                table: "score_processing_queue",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_score_task_queue_ScoreId",
                table: "score_task_queue",
                column: "ScoreId");

            migrationBuilder.CreateIndex(
                name: "IX_score_task_queue_ScoreProcessingQueueId",
                table: "score_task_queue",
                column: "ScoreProcessingQueueId");

            migrationBuilder.CreateIndex(
                name: "IX_score_task_queue_Status_LeaseExpiresAt",
                table: "score_task_queue",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_score_task_queue_Status_Priority_NextRetryAt",
                table: "score_task_queue",
                columns: new[] { "Status", "Priority", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_score_task_queue_TaskType_ScoreId",
                table: "score_task_queue",
                columns: new[] { "TaskType", "ScoreId" });

            migrationBuilder.CreateIndex(
                name: "UX_score_task_queue_active_payload",
                table: "score_task_queue",
                column: "ActiveScoreProcessingQueueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_score_task_queue_active_score",
                table: "score_task_queue",
                column: "ActiveScoreId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_task_queue");

            migrationBuilder.DropTable(
                name: "score_processing_queue");

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
