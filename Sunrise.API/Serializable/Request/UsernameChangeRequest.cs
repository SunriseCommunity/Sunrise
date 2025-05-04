using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class UsernameChangeRequest
{
    [JsonPropertyName("new_username")]
    public required string NewUsername { get; set; }
}