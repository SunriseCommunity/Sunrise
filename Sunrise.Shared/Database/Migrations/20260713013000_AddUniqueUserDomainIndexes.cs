using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations;

public partial class AddUniqueUserDomainIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE duplicate
            FROM user_medals AS duplicate
            INNER JOIN user_medals AS original
                ON duplicate.UserId = original.UserId
                AND duplicate.MedalId = original.MedalId
                AND duplicate.Id > original.Id;
            """);

        migrationBuilder.Sql("""
            DELETE duplicate
            FROM user_metadata AS duplicate
            INNER JOIN user_metadata AS original
                ON duplicate.UserId = original.UserId
                AND duplicate.Id < original.Id;
            """);

        migrationBuilder.Sql("""
            DELETE duplicate
            FROM user_relationship AS duplicate
            INNER JOIN user_relationship AS original
                ON duplicate.UserId = original.UserId
                AND duplicate.TargetId = original.TargetId
                AND duplicate.Id < original.Id;
            """);

        migrationBuilder.Sql("""
            DELETE duplicate
            FROM user_stats_snapshot AS duplicate
            INNER JOIN user_stats_snapshot AS original
                ON duplicate.UserId = original.UserId
                AND duplicate.GameMode = original.GameMode
                AND duplicate.Id < original.Id;
            """);

        migrationBuilder.CreateIndex(
            name: "UX_user_medals_UserId_MedalId",
            table: "user_medals",
            columns: new[] { "UserId", "MedalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_user_metadata_UserId",
            table: "user_metadata",
            column: "UserId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_user_relationship_UserId_TargetId",
            table: "user_relationship",
            columns: new[] { "UserId", "TargetId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_user_stats_snapshot_UserId_GameMode",
            table: "user_stats_snapshot",
            columns: new[] { "UserId", "GameMode" },
            unique: true);

        migrationBuilder.DropIndex(name: "IX_user_medals_UserId", table: "user_medals");
        migrationBuilder.DropIndex(name: "IX_user_metadata_UserId", table: "user_metadata");
        migrationBuilder.DropIndex(name: "IX_user_relationship_UserId_TargetId", table: "user_relationship");
        migrationBuilder.DropIndex(name: "IX_user_stats_snapshot_UserId_GameMode", table: "user_stats_snapshot");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(name: "IX_user_medals_UserId", table: "user_medals", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_user_metadata_UserId", table: "user_metadata", column: "UserId");
        migrationBuilder.CreateIndex(
            name: "IX_user_relationship_UserId_TargetId",
            table: "user_relationship",
            columns: new[] { "UserId", "TargetId" });
        migrationBuilder.CreateIndex(
            name: "IX_user_stats_snapshot_UserId_GameMode",
            table: "user_stats_snapshot",
            columns: new[] { "UserId", "GameMode" });

        migrationBuilder.DropIndex(name: "UX_user_medals_UserId_MedalId", table: "user_medals");
        migrationBuilder.DropIndex(name: "UX_user_metadata_UserId", table: "user_metadata");
        migrationBuilder.DropIndex(name: "UX_user_relationship_UserId_TargetId", table: "user_relationship");
        migrationBuilder.DropIndex(name: "UX_user_stats_snapshot_UserId_GameMode", table: "user_stats_snapshot");
    }
}
