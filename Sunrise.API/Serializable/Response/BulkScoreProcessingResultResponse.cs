using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class BulkScoreProcessingResultResponse(int queued, int skipped)
{
    [JsonPropertyName("queued")]
    public int Queued { get; set; } = queued;

    [JsonPropertyName("skipped")]
    public int Skipped { get; set; } = skipped;
}
