using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMetadataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_metadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Playstyle = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Interest = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Occupation = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Telegram = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Twitch = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Twitter = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Discord = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    Website = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_metadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_metadata_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_metadata_UserId",
                table: "user_metadata",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_metadata");
        }
    }
}
