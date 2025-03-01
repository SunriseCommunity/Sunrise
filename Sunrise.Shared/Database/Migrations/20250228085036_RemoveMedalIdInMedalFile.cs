using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMedalIdInMedalFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_medal_file_medal_MedalId",
                table: "medal_file");

            migrationBuilder.DropIndex(
                name: "IX_medal_file_MedalId",
                table: "medal_file");

            migrationBuilder.DropColumn(
                name: "MedalId",
                table: "medal_file");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MedalId",
                table: "medal_file",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_medal_file_MedalId",
                table: "medal_file",
                column: "MedalId");

            migrationBuilder.AddForeignKey(
                name: "FK_medal_file_medal_MedalId",
                table: "medal_file",
                column: "MedalId",
                principalTable: "medal",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
