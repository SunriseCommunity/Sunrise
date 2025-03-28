using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class MigrateToMySQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "medal_file",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Path = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medal_file", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Username = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_unicode_ci"),
                    Email = table.Column<string>(type: "varchar(255)", nullable: false, collation: "utf8mb4_unicode_ci"),
                    Passhash = table.Column<string>(type: "longtext", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: true),
                    Country = table.Column<short>(type: "smallint", nullable: false),
                    Privilege = table.Column<int>(type: "int", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastOnlineTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Friends = table.Column<string>(type: "longtext", nullable: false),
                    AccountStatus = table.Column<int>(type: "int", nullable: false),
                    SilencedUntil = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "medal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: false),
                    GameMode = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    FileUrl = table.Column<string>(type: "longtext", nullable: true),
                    FileId = table.Column<int>(type: "int", nullable: true),
                    Condition = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_medal_medal_file_FileId",
                        column: x => x.FileId,
                        principalTable: "medal_file",
                        principalColumn: "Id");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "event_user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    Ip = table.Column<string>(type: "varchar(255)", nullable: false),
                    JsonData = table.Column<string>(type: "longtext", nullable: false),
                    Time = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_user", x => x.Id);
                    table.ForeignKey(
                        name: "FK_event_user_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "restriction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ExecutorId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "longtext", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restriction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_restriction_user_ExecutorId",
                        column: x => x.ExecutorId,
                        principalTable: "user",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_restriction_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_favourite_beatmap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BeatmapSetId = table.Column<int>(type: "int", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_favourite_beatmap", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_favourite_beatmap_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_file",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "longtext", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_file", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_file_user_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_medals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    MedalId = table.Column<int>(type: "int", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_medals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_medals_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GameMode = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Accuracy = table.Column<double>(type: "double", nullable: false),
                    TotalScore = table.Column<long>(type: "BIGINT", nullable: false),
                    RankedScore = table.Column<long>(type: "BIGINT", nullable: false),
                    PlayCount = table.Column<int>(type: "int", nullable: false),
                    PerformancePoints = table.Column<double>(type: "double", nullable: false),
                    MaxCombo = table.Column<int>(type: "int", nullable: false),
                    PlayTime = table.Column<int>(type: "int", nullable: false),
                    TotalHits = table.Column<int>(type: "int", nullable: false),
                    BestGlobalRank = table.Column<long>(type: "BIGINT", nullable: true),
                    BestGlobalRankDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BestCountryRank = table.Column<long>(type: "BIGINT", nullable: true),
                    BestCountryRankDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_stats_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_stats_snapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GameMode = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    SnapshotsJson = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats_snapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_stats_snapshot_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "score",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BeatmapId = table.Column<int>(type: "int", nullable: false),
                    ScoreHash = table.Column<string>(type: "longtext", nullable: false),
                    BeatmapHash = table.Column<string>(type: "longtext", nullable: false),
                    ReplayFileId = table.Column<int>(type: "int", nullable: true),
                    TotalScore = table.Column<long>(type: "BIGINT", nullable: false),
                    MaxCombo = table.Column<int>(type: "int", nullable: false),
                    Count300 = table.Column<int>(type: "int", nullable: false),
                    Count100 = table.Column<int>(type: "int", nullable: false),
                    Count50 = table.Column<int>(type: "int", nullable: false),
                    CountMiss = table.Column<int>(type: "int", nullable: false),
                    CountKatu = table.Column<int>(type: "int", nullable: false),
                    CountGeki = table.Column<int>(type: "int", nullable: false),
                    Perfect = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Mods = table.Column<int>(type: "int", nullable: false),
                    Grade = table.Column<string>(type: "longtext", nullable: false),
                    IsPassed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsScoreable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SubmissionStatus = table.Column<int>(type: "int", nullable: false),
                    GameMode = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    WhenPlayed = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OsuVersion = table.Column<string>(type: "longtext", nullable: false),
                    BeatmapStatus = table.Column<int>(type: "int", nullable: false),
                    ClientTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Accuracy = table.Column<double>(type: "double", nullable: false),
                    PerformancePoints = table.Column<double>(type: "double", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score", x => x.Id);
                    table.ForeignKey(
                        name: "FK_score_user_UserId",
                        column: x => x.UserId,
                        principalTable: "user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_score_user_file_ReplayFileId",
                        column: x => x.ReplayFileId,
                        principalTable: "user_file",
                        principalColumn: "Id");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_event_user_EventType_UserId",
                table: "event_user",
                columns: new[] { "EventType", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_event_user_Ip",
                table: "event_user",
                column: "Ip");

            migrationBuilder.CreateIndex(
                name: "IX_event_user_UserId",
                table: "event_user",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_medal_Category",
                table: "medal",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_medal_FileId",
                table: "medal",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_medal_GameMode",
                table: "medal",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_restriction_ExecutorId",
                table: "restriction",
                column: "ExecutorId");

            migrationBuilder.CreateIndex(
                name: "IX_restriction_UserId",
                table: "restriction",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapId",
                table: "score",
                column: "BeatmapId");

            migrationBuilder.CreateIndex(
                name: "IX_score_BeatmapStatus",
                table: "score",
                column: "BeatmapStatus");

            migrationBuilder.CreateIndex(
                name: "IX_score_GameMode",
                table: "score",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_score_Mods",
                table: "score",
                column: "Mods");

            migrationBuilder.CreateIndex(
                name: "IX_score_PerformancePoints",
                table: "score",
                column: "PerformancePoints");

            migrationBuilder.CreateIndex(
                name: "IX_score_ReplayFileId",
                table: "score",
                column: "ReplayFileId");

            migrationBuilder.CreateIndex(
                name: "IX_score_SubmissionStatus",
                table: "score",
                column: "SubmissionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_score_TotalScore",
                table: "score",
                column: "TotalScore");

            migrationBuilder.CreateIndex(
                name: "IX_score_UserId",
                table: "score",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_AccountStatus",
                table: "user",
                column: "AccountStatus");

            migrationBuilder.CreateIndex(
                name: "IX_user_Email",
                table: "user",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_Username",
                table: "user",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_favourite_beatmap_BeatmapSetId",
                table: "user_favourite_beatmap",
                column: "BeatmapSetId");

            migrationBuilder.CreateIndex(
                name: "IX_user_favourite_beatmap_UserId",
                table: "user_favourite_beatmap",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_file_OwnerId",
                table: "user_file",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_user_file_Type",
                table: "user_file",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_user_medals_UserId",
                table: "user_medals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_GameMode",
                table: "user_stats",
                column: "GameMode");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_UserId",
                table: "user_stats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_snapshot_UserId_GameMode",
                table: "user_stats_snapshot",
                columns: new[] { "UserId", "GameMode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_user");

            migrationBuilder.DropTable(
                name: "medal");

            migrationBuilder.DropTable(
                name: "restriction");

            migrationBuilder.DropTable(
                name: "score");

            migrationBuilder.DropTable(
                name: "user_favourite_beatmap");

            migrationBuilder.DropTable(
                name: "user_medals");

            migrationBuilder.DropTable(
                name: "user_stats");

            migrationBuilder.DropTable(
                name: "user_stats_snapshot");

            migrationBuilder.DropTable(
                name: "medal_file");

            migrationBuilder.DropTable(
                name: "user_file");

            migrationBuilder.DropTable(
                name: "user");
        }
    }
}
