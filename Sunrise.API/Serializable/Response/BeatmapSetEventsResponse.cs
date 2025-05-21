using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class BeatmapSetEventsResponse(List<BeatmapEventResponse> events, int totalCount)
{
    [JsonPropertyName("events")]
    public List<BeatmapEventResponse> Events { get; set; } = events;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}