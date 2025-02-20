using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class FriendStatusResponse(bool isFollowingYou, bool isFollowedByYou)
{
    [JsonPropertyName("is_following_you")]
    public bool IsFollowingYou { get; set; } = isFollowingYou;

    [JsonPropertyName("is_followed_by_you")]
    public bool IsFollowedByYou { get; set; } = isFollowedByYou;
}