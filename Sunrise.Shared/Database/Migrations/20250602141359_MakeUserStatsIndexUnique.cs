using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserStatsIndexUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_stats_UserId_GameMode",
                table: "user_stats");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_UserId_GameMode",
                table: "user_stats",
                columns: new[] { "UserId", "GameMode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_stats_UserId_GameMode",
                table: "user_stats");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_UserId_GameMode",
                table: "user_stats",
                columns: new[] { "UserId", "GameMode" });
        }
    }
}
