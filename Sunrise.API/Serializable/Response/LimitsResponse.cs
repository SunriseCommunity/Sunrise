using System.Text.Json.Serialization;
using Sunrise.Shared.Application;

namespace Sunrise.API.Serializable.Response;

public class RateLimits
{
    [JsonPropertyName("total_limit")] public int TotalLimit { get; set; }

    [JsonPropertyName("remaining_calls")] public long RemainingCalls { get; set; }
}

public class LimitsResponse
{
    private static int BeatmapRequestLimit => Configuration.ApiCallsPerWindow;

    private static int TotalLimit => Configuration.GeneralCallsPerWindow;

    public LimitsResponse(long? remainingCallsValue, int? remainingBeatmapRequestsValue)
    {
        var remainingCalls = remainingCallsValue ?? Configuration.GeneralCallsPerWindow;
        var remainingBeatmapRequests = remainingBeatmapRequestsValue ?? Configuration.ApiCallsPerWindow;
       
        RateLimitsObj = new RateLimits
        {
            TotalLimit = TotalLimit,
            RemainingCalls = remainingCalls
        };
       
        RateLimitsBeatmap  = new RateLimits
        {
            TotalLimit = BeatmapRequestLimit,
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