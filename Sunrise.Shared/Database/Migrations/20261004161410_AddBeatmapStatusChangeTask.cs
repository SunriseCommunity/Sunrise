using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBeatmapStatusChangeTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreviousStatus",
                table: "beatmap_hash_status",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ActiveScoreId",
                table: "score_processing_task",
                type: "int",
                nullable: true,
                computedColumnSql: "CASE WHEN Status IN (0, 1) AND TaskType <> 4 THEN ScoreId ELSE NULL END",
                stored: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComputedColumnSql: "CASE WHEN Status IN (0, 1) THEN ScoreId ELSE NULL END",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "beatmap_hash_status");

            migrationBuilder.AlterColumn<int>(
                name: "ActiveScoreId",
                table: "score_processing_task",
                type: "int",
                nullable: true,
                computedColumnSql: "CASE WHEN Status IN (0, 1) THEN ScoreId ELSE NULL END",
                stored: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldComputedColumnSql: "CASE WHEN Status IN (0, 1) AND TaskType <> 4 THEN ScoreId ELSE NULL END",
                oldStored: true);
        }
    }
}
