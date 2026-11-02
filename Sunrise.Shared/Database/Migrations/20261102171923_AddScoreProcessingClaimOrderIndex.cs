using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreProcessingClaimOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_score_processing_task_claim_order",
                table: "score_processing_task",
                columns: new[] { "Priority", "CreatedAt", "Id", "Status", "NextRetryAt", "LeaseExpiresAt" },
                descending: new[] { true, false, false, false, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_score_processing_task_claim_order",
                table: "score_processing_task");
        }
    }
}
