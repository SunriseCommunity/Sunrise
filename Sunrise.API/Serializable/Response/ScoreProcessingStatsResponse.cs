using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class ScoreProcessingStatsResponse
{
    [JsonConstructor]
    public ScoreProcessingStatsResponse()
    {
    }

    public ScoreProcessingStatsResponse(long pending, long processing, long failed, double? estimatedPendingCompletionSeconds)
    {
        Pending = pending;
        Processing = processing;
        Failed = failed;
        EstimatedPendingCompletionSeconds = estimatedPendingCompletionSeconds;
    }

    [JsonPropertyName("pending")]
    public long Pending { get; set; }

    [JsonPropertyName("processing")]
    public long Processing { get; set; }

    [JsonPropertyName("failed")]
    public long Failed { get; set; }

    [JsonPropertyName("estimated_pending_completion_seconds")]
    public double? EstimatedPendingCompletionSeconds { get; set; }
}