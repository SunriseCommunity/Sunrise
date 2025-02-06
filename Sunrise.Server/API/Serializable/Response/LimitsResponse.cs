using System.Text.Json.Serialization;
using Sunrise.Server.Application;

namespace Sunrise.Server.API.Serializable.Response;

public class RateLimits
{
    [JsonPropertyName("total_limit")] public int TotalLimit { get; set; }

    [JsonPropertyName("remaining_calls")] public long RemainingCalls { get; set; }
}

public class LimitsResponse
{
    private readonly int _beatmapRequestLimit = Configuration.ApiCallsPerWindow;

    private readonly int _totalLimit = Configuration.GeneralCallsPerWindow;

    public LimitsResponse(long? remainingCallsValue, int? remainingBeatmapRequestsValue)
    {
        var remainingCalls = remainingCallsValue ?? Configuration.GeneralCallsPerWindow;
        var remainingBeatmapRequests = remainingBeatmapRequestsValue ?? Configuration.ApiCallsPerWindow;
       
        RateLimitsObj = new RateLimits
        {
            TotalLimit = _totalLimit,
            RemainingCalls = remainingCalls
        };
       
        RateLimitsBeatmap  = new RateLimits
        {
            TotalLimit = _beatmapRequestLimit,
            RemainingCalls = remainingBeatmapRequests
        };
    }
    
    [JsonConstructor]
    public LimitsResponse(RateLimits rateLimitsObj, RateLimits rateLimitsBeatmap, string message)
    {
        RateLimitsObj = rateLimitsObj;
        RateLimitsBeatmap = rateLimitsBeatmap;
        Message = message;
    }

    [JsonPropertyName("message")]
    public string Message { get; set; } =
        "Total limits are any requests to the server. Beatmap request limits are requests which involve beatmap retrieval. Keep in mind that beatmap requests only counts towards new beatmaps to our database.";

    [JsonPropertyName("rate_limits")]
    public RateLimits RateLimitsObj { get; set; }

    [JsonPropertyName("beatmap_rate_limits")]
    public RateLimits RateLimitsBeatmap { get; set; } 
}