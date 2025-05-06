using System.Text.Json.Serialization;
using Sunrise.API.Enums;

namespace Sunrise.API.Serializable.Request;

public class EditFriendshipStatusRequest
{
    [JsonPropertyName("action")]
    public required UpdateFriendshipStatusAction Action { get; set; }
}