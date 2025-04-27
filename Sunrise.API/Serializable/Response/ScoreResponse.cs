using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Extensions.Scores;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class ScoreResponse
{

    [JsonConstructor]
    public ScoreResponse()
    {
    }

    public ScoreResponse(SessionRepository sessionRepository, Score score)
    {
        BeatmapId = score.BeatmapId;
        Count100 = score.Count100;
        Count300 = score.Count300;
        Count50 = score.Count50;
        CountGeki = score.CountGeki;
        CountKatu = score.CountKatu;
        CountMiss = score.CountMiss;
        GameMode = (GameMode)score.GameMode.ToVanillaGameMode();
        GameModeExtended = score.GameMode;
        Grade = score.Grade;
        Id = score.Id;
        IsPassed = score.IsPassed;
        HasReplay = score.ReplayFileId != null;
        LeaderboardRank = score.LocalProperties.LeaderboardPosition;
        MaxCombo = score.MaxCombo;
        Mods = score.Mods.GetModsString();
        ModsInt = (int)score.Mods;
        Perfect = score.Perfect;
        PerformancePoints = score.PerformancePoints;
        TotalScore = score.TotalScore;
        UserId = score.UserId;
        WhenPlayed = score.WhenPlayed;
        User = new UserResponse(sessionRepository, score.User);
        Accuracy = score.Accuracy;
    }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("beatmap_id")]
    public int BeatmapId { get; set; }

    [JsonPropertyName("count_100")]
    public int Count100 { get; set; }

    [JsonPropertyName("count_300")]
    public int Count300 { get; set; }

    [JsonPropertyName("count_50")]
    public int Count50 { get; set; }

    [JsonPropertyName("count_geki")]
    public int CountGeki { get; set; }

    [JsonPropertyName("count_katu")]
    public int CountKatu { get; set; }

    [JsonPropertyName("count_miss")]
    public int CountMiss { get; set; }

    [JsonPropertyName("game_mode")]
    public GameMode GameMode { get; set; }

    [JsonPropertyName("game_mode_extended")]
    public GameMode GameModeExtended { get; set; }

    [JsonPropertyName("grade")]
    public string Grade { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("is_passed")]
    public bool IsPassed { get; set; }

    [JsonPropertyName("has_replay")]
    public bool HasReplay { get; set; }

    [JsonPropertyName("leaderboard_rank")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LeaderboardRank { get; set; }

    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; }

    [JsonPropertyName("mods")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mods { get; set; }

    [JsonPropertyName("mods_int")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ModsInt { get; set; }

    [JsonPropertyName("is_perfect")]
    public bool Perfect { get; set; }

    [JsonPropertyName("performance_points")]
    public double PerformancePoints { get; set; }

    [JsonPropertyName("total_score")]
    public long TotalScore { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("when_played")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime WhenPlayed { get; set; }

    [JsonPropertyName("user")]
    public UserResponse User { get; set; }
}