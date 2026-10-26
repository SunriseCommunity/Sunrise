using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class EventScoreProcessingListResponse(List<EventScoreProcessingResponse> events, int totalCount)
{
    [JsonPropertyName("events")]
    public List<EventScoreProcessingResponse> Events { get; set; } = events;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}
