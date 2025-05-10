using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Utils.Converters;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;

namespace Sunrise.API.Serializable.Response;

public class UserStatsResponse
{
    public UserStatsResponse(UserStats stats, long globalRank, long countryRank)
    {
        UserId = stats.UserId;
        GameMode = stats.GameMode;
        Accuracy = stats.Accuracy;
        TotalScore = stats.TotalScore;
        RankedScore = stats.RankedScore;
        PlayCount = stats.PlayCount;
        PerformancePoints = stats.PerformancePoints;
        Rank = globalRank;
        CountryRank = countryRank;
        MaxCombo = stats.MaxCombo;
        PlayTime = stats.PlayTime;
        TotalHits = stats.TotalHits;
        BestGlobalRank = stats.BestGlobalRank ?? globalRank;
        BestGlobalRankDate = stats.BestGlobalRankDate ?? DateTime.UtcNow;
        BestCountryRank = stats.BestCountryRank ?? countryRank;
        BestCountryRankDate = stats.BestCountryRankDate ?? DateTime.UtcNow;
    }

    [JsonConstructor]
    public UserStatsResponse() { }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("gamemode")]
    public GameMode GameMode { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("total_score")]
    public long TotalScore { get; set; }

    [JsonPropertyName("ranked_score")]
    public long RankedScore { get; set; }

    [JsonPropertyName("play_count")]
    public int PlayCount { get; set; }

    [JsonPropertyName("pp")]
    public double PerformancePoints { get; set; }

    [JsonPropertyName("rank")]
    public long Rank { get; set; }

    [JsonPropertyName("country_rank")]
    public long CountryRank { get; set; }

    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; }

    [JsonPropertyName("play_time")]
    public int PlayTime { get; set; }

    [JsonPropertyName("total_hits")]
    public int TotalHits { get; set; }

    [JsonPropertyName("best_global_rank")]
    public long BestGlobalRank { get; set; }

    [JsonPropertyName("best_global_rank_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime BestGlobalRankDate { get; set; }

    [JsonPropertyName("best_country_rank")]
    public long BestCountryRank { get; set; }

    [JsonPropertyName("best_country_rank_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime BestCountryRankDate { get; set; }
}