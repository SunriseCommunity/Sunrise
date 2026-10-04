using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreProcessingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_score_processing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ExecutorId = table.Column<int>(type: "int", nullable: true),
                    ScoreId = table.Column<int>(type: "int", nullable: true),
                    TaskId = table.Column<int>(type: "int", nullable: true),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    JsonData = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Time = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_score_processing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_score_processing_score_ScoreId",
                        column: x => x.ScoreId,
                        principalTable: "score",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_event_score_processing_user_ExecutorId",
                        column: x => x.ExecutorId,
                        principalTable: "user",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_event_score_processing_EventType",
                table: "event_score_processing",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_event_score_processing_ExecutorId",
                table: "event_score_processing",
                column: "ExecutorId");

            migrationBuilder.CreateIndex(
                name: "IX_event_score_processing_ScoreId",
                table: "event_score_processing",
                column: "ScoreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_score_processing");
        }
    }
}
