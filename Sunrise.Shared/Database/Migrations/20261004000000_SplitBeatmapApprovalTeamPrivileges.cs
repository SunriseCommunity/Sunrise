using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Sunrise.Shared.Database.Migrations;

[DbContext(typeof(SunriseDbContext))]
[Migration("20261004000000_SplitBeatmapApprovalTeamPrivileges")]
public partial class SplitBeatmapApprovalTeamPrivileges : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE user
            SET Privilege = (Privilege & ~2) | 30720
            WHERE (Privilege & 2) = 2;
            ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE user
            SET Privilege = (Privilege & ~30720) | 2
            WHERE (Privilege & 30720) != 0;
            ");
    }
}
