using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class EventUsersResponse(List<EventUserResponse> events, int totalCount)
{
    [JsonPropertyName("events")]
    public List<EventUserResponse> Events { get; set; } = events;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}
