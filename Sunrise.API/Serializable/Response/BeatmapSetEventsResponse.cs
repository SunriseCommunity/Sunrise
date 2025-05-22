using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class BeatmapSetEventsResponse
{
    [JsonConstructor]
    public BeatmapSetEventsResponse() { }

    public BeatmapSetEventsResponse(List<BeatmapEventResponse> events, int totalCount)
    {
        Events = events;
        TotalCount = totalCount;
    }

    [JsonPropertyName("events")]
    public List<BeatmapEventResponse> Events { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}