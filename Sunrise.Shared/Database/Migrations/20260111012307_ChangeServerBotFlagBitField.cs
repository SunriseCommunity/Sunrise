using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeServerBotFlagBitField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            UPDATE user
            SET Privilege = (Privilege & ~32) | 1024
            WHERE (Privilege & 32) = 32;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            UPDATE user
            SET Privilege = (Privilege & ~1024) | 32
            WHERE (Privilege & 32) = 32;
            ");
        }
    }
}
