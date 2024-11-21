using System.Text.Json.Serialization;
using osu.Shared;
using Sunrise.Server.Database.Models.User;

namespace Sunrise.Server.API.Serializable.Response;

public class UserStatsResponse(UserStats stats, int globalRank, int countryRank)
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
    public double PerformancePoints { get; set; } = stats.PerformancePoints;

    [JsonPropertyName("rank")]
    public int Rank { get; set; } = globalRank;

    [JsonPropertyName("country_rank")]
    public int CountryRank { get; set; } = countryRank;

    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; } = stats.MaxCombo;

    [JsonPropertyName("play_time")]
    public int PlayTime { get; set; } = stats.PlayTime;

    [JsonPropertyName("total_hits")]
    public int TotalHits { get; set; } = stats.TotalHits;

    [JsonPropertyName("best_global_rank")]
    public long? BestGlobalRank { get; set; } = stats.BestGlobalRank;

    [JsonPropertyName("best_global_rank_date")]
    public DateTime? BestGlobalRankDate { get; set; } = stats.BestGlobalRankDate;

    [JsonPropertyName("best_country_rank")]
    public long? BestCountryRank { get; set; } = stats.BestCountryRank;

    [JsonPropertyName("best_country_rank_date")]
    public DateTime? BestCountryRankDate { get; set; } = stats.BestCountryRankDate;
}