using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class TokenRequest
{
    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}