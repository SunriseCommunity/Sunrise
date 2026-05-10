using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class LimitScoreHashTo32CharactersForScoreProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ScoreHash",
                table: "score_processing_queue",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ScoreHash",
                table: "score_processing_queue",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(32)",
                oldMaxLength: 32);
        }
    }
}
