using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddNewTablesForScoreDataRowMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "score_hits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    MaxCombo = table.Column<int>(type: "int", nullable: false),
                    Count300 = table.Column<int>(type: "int", nullable: false),
                    Count100 = table.Column<int>(type: "int", nullable: false),
                    Count50 = table.Column<int>(type: "int", nullable: false),
                    CountMiss = table.Column<int>(type: "int", nullable: false),
                    CountKatu = table.Column<int>(type: "int", nullable: false),
                    CountGeki = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_hits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_score_hits_score_Id",
                        column: x => x.Id,
                        principalTable: "score",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "score_metadata",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    ScoreHash = table.Column<string>(type: "varchar(255)", nullable: false),
                    OsuVersion = table.Column<string>(type: "longtext", nullable: false),
                    ClientTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score_metadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_score_metadata_score_Id",
                        column: x => x.Id,
                        principalTable: "score",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_score_metadata_ScoreHash",
                table: "score_metadata",
                column: "ScoreHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_hits");

            migrationBuilder.DropTable(
                name: "score_metadata");
        }
    }
}
