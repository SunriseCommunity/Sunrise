using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_grades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GameMode = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    CountXH = table.Column<int>(type: "int", nullable: false),
                    CountX = table.Column<int>(type: "int", nullable: false),
                    CountSH = table.Column<int>(type: "int", nullable: false),
                    CountS = table.Column<int>(type: "int", nullable: false),
                    CountA = table.Column<int>(type: "int", nullable: false),
                    CountB = table.Column<int>(type: "int", nullable: false),
                    CountC = table.Column<int>(type: "int", nullable: false),
                    CountD = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_grades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_grades_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_user_grades_UserId_GameMode",
                table: "user_grades",
                columns: new[] { "UserId", "GameMode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_grades");
        }
    }
}
