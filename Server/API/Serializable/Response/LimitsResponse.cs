using System.Text.Json.Serialization;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Serializable.Response;

public class LimitsResponse(long? remainingCalls, int? remainingBeatmapRequests)
{
    private readonly int _beatmapRequestLimit = Configuration.ApiCallsPerWindow;
    private readonly int _remainingBeatmapRequests = remainingBeatmapRequests ?? Configuration.ApiCallsPerWindow;
    private readonly long _remainingCalls = remainingCalls ?? Configuration.GeneralCallsPerWindow;
    private readonly int _totalLimit = Configuration.GeneralCallsPerWindow;

    [JsonPropertyName("message")]
    public string Message { get; } =
        "Total limits are any requests to the server. Beatmap request limits are requests which involve beatmap retrieval. Keep in mind that beatmap requests only counts towards new beatmaps to our database.";

    [JsonPropertyName("rate_limits")]
    public RateLimits RateLimitsObj => new()
    {
        TotalLimit = _totalLimit,
        RemainingCalls = _remainingCalls
    };

    [JsonPropertyName("beatmap_rate_limits")]
    public RateLimits RateLimitsBeatmap => new()
    {
        TotalLimit = _beatmapRequestLimit,
        RemainingCalls = _remainingBeatmapRequests
    };

    public class RateLimits
    {
        [JsonPropertyName("total_limit")] public int TotalLimit { get; set; }

        [JsonPropertyName("remaining_calls")] public long RemainingCalls { get; set; }
    }
}