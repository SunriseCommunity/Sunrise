using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixAllBadBestValuesForUserStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            UPDATE user_stats
            SET BestGlobalRankDate = NULL
            WHERE BestGlobalRankDate = '0001-01-01 00:00:00';

            UPDATE user_stats
            SET BestCountryRankDate = NULL
            WHERE BestCountryRankDate = '0001-01-01 00:00:00';

            UPDATE user_stats
            SET BestGlobalRank = NULL
            WHERE BestGlobalRank = 0;

            UPDATE user_stats
            SET BestCountryRank = NULL
            WHERE BestCountryRank = 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            UPDATE user_stats
            SET BestGlobalRankDate = '0001-01-01 00:00:00'
            WHERE BestGlobalRankDate IS NULL;

            UPDATE user_stats
            SET BestCountryRankDate = '0001-01-01 00:00:00'
            WHERE BestCountryRankDate IS NULL;

            UPDATE user_stats
            SET BestGlobalRank = 0
            WHERE BestGlobalRank IS NULL;

            UPDATE user_stats
            SET BestCountryRank = 0
            WHERE BestCountryRank IS NULL;
            ");
        }
    }
}
