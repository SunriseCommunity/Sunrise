using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class FollowersResponse(List<UserResponse> followers, int totalCount)
{
    [JsonPropertyName("followers")]
    public List<UserResponse> Followers { get; set; } = followers;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}