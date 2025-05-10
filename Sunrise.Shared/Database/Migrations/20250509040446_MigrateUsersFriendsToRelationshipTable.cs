using Microsoft.EntityFrameworkCore.Migrations;
using Sunrise.Shared.Enums.Users;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class MigrateUsersFriendsToRelationshipTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var query = $"""
                          INSERT INTO user_relationship (UserId, TargetId, Relation)
                          SELECT
                          u.Id AS UserId,
                          CAST(f.value AS SIGNED) AS TargetId,
                          {(int)UserRelation.Friend} AS Relation
                          FROM user u
                          JOIN JSON_TABLE(
                          CONCAT('["', REPLACE(TRIM(BOTH ',' FROM u.Friends), ',', '","'), '"]'),
                          '$[*]' COLUMNS (
                          value VARCHAR(255) PATH '$'
                          )
                          ) AS f
                          WHERE u.Friends IS NOT NULL
                          AND TRIM(u.Friends) <> ''
                          AND f.value REGEXP '^[0-9]+$';
                          """;
            
            migrationBuilder.Sql(query);
        }
                
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
                