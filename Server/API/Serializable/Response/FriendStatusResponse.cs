using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Response;

public class FriendStatusResponse(bool isFollowing, bool isFollowed)
{
    [JsonPropertyName("is_following")]
    public bool IsFollowing { get; set; } = isFollowing;

    [JsonPropertyName("is_followed")]
    public bool IsFollowed { get; set; } = isFollowed;
}
