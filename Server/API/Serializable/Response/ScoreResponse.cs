using System.Text.Json.Serialization;
using osu.Shared;
using Sunrise.Server.Database.Models;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Serializable.Response;

public class ScoreResponse(Score score)
{
    [JsonPropertyName("accuracy")] public double Accuracy { get; set; } = score.Accuracy;

    [JsonPropertyName("beatmap_id")] public int BeatmapId { get; set; } = score.BeatmapId;

    [JsonPropertyName("count_100")] public int Count100 { get; set; } = score.Count100;

    [JsonPropertyName("count_300")] public int Count300 { get; set; } = score.Count300;

    [JsonPropertyName("count_50")] public int Count50 { get; set; } = score.Count50;

    [JsonPropertyName("count_geki")] public int CountGeki { get; set; } = score.CountGeki;

    [JsonPropertyName("count_katu")] public int CountKatu { get; set; } = score.CountKatu;

    [JsonPropertyName("count_miss")] public int CountMiss { get; set; } = score.CountMiss;

    [JsonPropertyName("game_mode")] public GameMode GameMode { get; set; } = score.GameMode;

    [JsonPropertyName("grade")] public string Grade { get; set; } = score.Grade;

    [JsonPropertyName("id")] public int Id { get; set; } = score.Id;

    [JsonPropertyName("is_passed")] public bool IsPassed { get; set; } = score.IsPassed;

    [JsonPropertyName("has_replay")] public bool HasReplay { get; set; } = score.ReplayFileId != null;

    [JsonPropertyName("leaderboard_rank")] public int LeaderboardRank { get; set; } = score.GetLeaderboardRank().Result;

    [JsonPropertyName("max_combo")] public int MaxCombo { get; set; } = score.MaxCombo;

    [JsonPropertyName("mods")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mods { get; set; } = score.Mods.GetModsString();

    [JsonPropertyName("is_perfect")] public bool Perfect { get; set; } = score.Perfect;

    [JsonPropertyName("performance_points")]
    public double PerformancePoints { get; set; } = score.PerformancePoints;

    [JsonPropertyName("total_score")] public int TotalScore { get; set; } = score.TotalScore;

    [JsonPropertyName("user_id")] public int UserId { get; set; } = score.UserId;

    [JsonPropertyName("when_played")] public DateTime WhenPlayed { get; set; } = score.WhenPlayed;
}