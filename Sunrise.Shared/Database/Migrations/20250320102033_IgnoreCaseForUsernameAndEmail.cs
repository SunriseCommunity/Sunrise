using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class IgnoreCaseForUsernameAndEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "user",
                type: "TEXT COLLATE NOCASE",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "user",
                type: "TEXT COLLATE NOCASE",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT COLLATE NOCASE");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "user",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT COLLATE NOCASE");
        }
    }
}
