using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class MigrateFromWatsonORM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "beatmap_file",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BeatmapId = table.Column<int>(type: "INTEGER", nullable: false),
                    BeatmapSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "VARCHAR(2147483647)", nullable: false, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beatmap_file", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "event_user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    Ip = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    JsonData = table.Column<string>(type: "VARCHAR(2147483647)", nullable: false, collation: "NOCASE"),
                    Time = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "medal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE"),
                    GameMode = table.Column<int>(type: "INTEGER", nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    FileUrl = table.Column<string>(type: "VARCHAR(1024)", nullable: true, collation: "NOCASE"),
                    FileId = table.Column<int>(type: "INTEGER", nullable: true),
                    Condition = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "medal_file",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MedalId = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medal_file", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "migration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE"),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "restriction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "VARCHAR(2147483647)", nullable: false, collation: "NOCASE"),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_restriction", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "score",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BeatmapId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreHash = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    BeatmapHash = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    ReplayFileId = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalScore = table.Column<long>(type: "BIGINT", nullable: false),
                    MaxCombo = table.Column<int>(type: "INTEGER", nullable: false),
                    Count300 = table.Column<int>(type: "INTEGER", nullable: false),
                    Count100 = table.Column<int>(type: "INTEGER", nullable: false),
                    Count50 = table.Column<int>(type: "INTEGER", nullable: false),
                    CountMiss = table.Column<int>(type: "INTEGER", nullable: false),
                    CountKatu = table.Column<int>(type: "INTEGER", nullable: false),
                    CountGeki = table.Column<int>(type: "INTEGER", nullable: false),
                    Perfect = table.Column<byte>(type: "TINYINT", nullable: false),
                    Mods = table.Column<int>(type: "INTEGER", nullable: false),
                    Grade = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    IsPassed = table.Column<byte>(type: "TINYINT", nullable: false),
                    IsScoreable = table.Column<byte>(type: "TINYINT", nullable: false),
                    SubmissionStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    GameMode = table.Column<int>(type: "INTEGER", nullable: false),
                    WhenPlayed = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OsuVersion = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    BeatmapStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Accuracy = table.Column<int>(type: "DECIMAL(100, 2)", nullable: false),
                    PerformancePoints = table.Column<decimal>(type: "DECIMAL(100, 2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_score", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    Email = table.Column<string>(type: "VARCHAR(1024)", nullable: false, collation: "NOCASE"),
                    Passhash = table.Column<string>(type: "VARCHAR(64)", nullable: false, collation: "NOCASE"),
                    Description = table.Column<string>(type: "VARCHAR(2147483647)", nullable: true, collation: "NOCASE"),
                    Country = table.Column<int>(type: "INTEGER", nullable: false),
                    Privilege = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastOnlineTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Friends = table.Column<string>(type: "VARCHAR(2147483647)", nullable: false, collation: "NOCASE"),
                    AccountStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    SilencedUntil = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_favourite_beatmap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    BeatmapSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_favourite_beatmap", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_file",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Path = table.Column<string>(type: "VARCHAR(2147483647)", nullable: false, collation: "NOCASE"),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_file", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_medals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    MedalId = table.Column<int>(type: "INTEGER", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_medals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Accuracy = table.Column<double>(type: "REAL", nullable: false),
                    TotalScore = table.Column<long>(type: "BIGINT", nullable: false),
                    RankedScore = table.Column<long>(type: "BIGINT", nullable: false),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PerformancePoints = table.Column<double>(type: "REAL", nullable: false),
                    MaxCombo = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayTime = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalHits = table.Column<int>(type: "INTEGER", nullable: false),
                    BestGlobalRank = table.Column<long>(type: "BIGINT", nullable: true),
                    BestGlobalRankDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BestCountryRank = table.Column<long>(type: "BIGINT", nullable: true),
                    BestCountryRankDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_stats_snapshot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    GameMode = table.Column<int>(type: "INTEGER", nullable: false),
                    SnapshotsJson = table.Column<string>(type: "VARCHAR(2147483647)", nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats_snapshot", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "beatmap_file");

            migrationBuilder.DropTable(
                name: "event_user");

            migrationBuilder.DropTable(
                name: "medal");

            migrationBuilder.DropTable(
                name: "medal_file");

            migrationBuilder.DropTable(
                name: "migration");

            migrationBuilder.DropTable(
                name: "restriction");

            migrationBuilder.DropTable(
                name: "score");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "user_favourite_beatmap");

            migrationBuilder.DropTable(
                name: "user_file");

            migrationBuilder.DropTable(
                name: "user_medals");

            migrationBuilder.DropTable(
                name: "user_stats");

            migrationBuilder.DropTable(
                name: "user_stats_snapshot");
        }
    }
}
