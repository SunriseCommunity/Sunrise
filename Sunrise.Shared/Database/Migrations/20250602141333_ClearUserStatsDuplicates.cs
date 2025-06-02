using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class ClearUserStatsDuplicates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                                    WITH RankedStats AS (
                                        SELECT *,
                                               ROW_NUMBER() OVER (PARTITION BY UserId, GameMode ORDER BY TotalScore DESC) AS rn
                                        FROM user_stats
                                    )
                                    DELETE FROM user_stats
                                    WHERE Id IN (
                                        SELECT Id
                                        FROM RankedStats
                                        WHERE rn > 1
                                    );
                                   ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
