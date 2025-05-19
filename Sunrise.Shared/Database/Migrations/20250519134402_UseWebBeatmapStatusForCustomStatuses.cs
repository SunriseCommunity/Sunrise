using Microsoft.EntityFrameworkCore.Migrations;
using Sunrise.Shared.Enums.Beatmaps;

#nullable disable

namespace Sunrise.Shared.Database.Migrations
{
    /// <inheritdoc />
    public partial class UseWebBeatmapStatusForCustomStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                                  UPDATE custom_beatmap_status SET Status = 
                                      CASE Status
                                          WHEN {(int)BeatmapStatus.Unknown} THEN {(int)BeatmapStatusWeb.Unknown} 
                                          WHEN {(int)BeatmapStatus.NotSubmitted} THEN {(int)BeatmapStatusWeb.Unknown} 
                                          WHEN {(int)BeatmapStatus.Pending} THEN {(int)BeatmapStatusWeb.Pending}   
                                          WHEN {(int)BeatmapStatus.NeedsUpdate} THEN {(int)BeatmapStatusWeb.Unknown}  
                                          WHEN {(int)BeatmapStatus.Ranked} THEN {(int)BeatmapStatusWeb.Ranked}  
                                          WHEN {(int)BeatmapStatus.Approved} THEN {(int)BeatmapStatusWeb.Approved}
                                          WHEN {(int)BeatmapStatus.Qualified} THEN {(int)BeatmapStatusWeb.Qualified}
                                          WHEN {(int)BeatmapStatus.Loved} THEN {(int)BeatmapStatusWeb.Loved}
                                      ELSE Status
                                  END;
                                  """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                                  UPDATE custom_beatmap_status SET Status = 
                                      CASE Status
                                            WHEN {(int)BeatmapStatusWeb.Unknown} THEN {(int)BeatmapStatus.Unknown}
                                            WHEN {(int)BeatmapStatusWeb.Unknown} THEN {(int)BeatmapStatus.NotSubmitted}
                                            WHEN {(int)BeatmapStatusWeb.Pending} THEN {(int)BeatmapStatus.Pending}
                                            WHEN {(int)BeatmapStatusWeb.Unknown} THEN {(int)BeatmapStatus.NeedsUpdate}
                                            WHEN {(int)BeatmapStatusWeb.Ranked} THEN {(int)BeatmapStatus.Ranked}
                                            WHEN {(int)BeatmapStatusWeb.Approved} THEN {(int)BeatmapStatus.Approved}
                                            WHEN {(int)BeatmapStatusWeb.Qualified} THEN {(int)BeatmapStatus.Qualified}
                                            WHEN {(int)BeatmapStatusWeb.Loved} THEN {(int)BeatmapStatus.Loved}
                                      ELSE Status
                                  END;
                                  """);
        }
    }
}
