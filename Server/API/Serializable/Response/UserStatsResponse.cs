using System.Text.Json.Serialization;
using osu.Shared;
using Sunrise.Server.Database.Models;

namespace Sunrise.Server.API.Serializable.Response;

public class UserStatsResponse(UserStats stats, int globalRank)
{
    [JsonPropertyName("user_id")]
    public int UserId { get; set; } = stats.UserId;

    [JsonPropertyName("gamemode")]
    public GameMode GameMode { get; set; } = stats.GameMode;

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; } = stats.Accuracy;

    [JsonPropertyName("total_score")]
    public long TotalScore { get; set; } = stats.TotalScore;

    [JsonPropertyName("ranked_score")]
    public long RankedScore { get; set; } = stats.RankedScore;

    [JsonPropertyName("play_count")]
    public int PlayCount { get; set; } = stats.PlayCount;

    [JsonPropertyName("pp")]
    public short PerformancePoints { get; set; } = stats.PerformancePoints;

    [JsonPropertyName("rank")]
    public int Rank { get; set; } = globalRank;

    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; } = stats.MaxCombo;

    [JsonPropertyName("play_time_ms")]
    public int PlayTime { get; set; } = stats.PlayTime;
}