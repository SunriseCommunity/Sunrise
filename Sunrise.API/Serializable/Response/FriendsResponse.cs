using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class FriendsResponse(List<UserResponse> friends, int totalCount)
{
    [JsonPropertyName("friends")]
    public List<UserResponse> Friends { get; set; } = friends;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}