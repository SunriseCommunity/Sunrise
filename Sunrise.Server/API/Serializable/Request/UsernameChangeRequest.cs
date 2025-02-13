using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class UsernameChangeRequest
{
    [JsonPropertyName("new_username")]
    [JsonRequired]
    public string? NewUsername { get; set; }
}