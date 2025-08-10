using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.API.Enums;

namespace Sunrise.API.Serializable.Request;

public class EditFriendshipStatusRequest
{
    [JsonPropertyName("action")]
    [Required]
    public required UpdateFriendshipStatusAction Action { get; set; }
}