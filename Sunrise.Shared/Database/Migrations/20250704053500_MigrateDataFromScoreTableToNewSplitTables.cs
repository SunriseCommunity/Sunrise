using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class MigrateDataFromScoreTableToNewSplitTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            INSERT INTO score_metadata (Id, ScoreHash, OsuVersion, ClientTime)
            SELECT Id, ScoreHash, OsuVersion, ClientTime FROM score;
            ");
            
            migrationBuilder.Sql(@"
            INSERT INTO score_hits (Id, MaxCombo, Count300, Count100, Count50, CountMiss, CountKatu, CountGeki) 
            SELECT Id, MaxCombo, Count300, Count100, Count50, CountMiss, CountKatu, CountGeki FROM score;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
