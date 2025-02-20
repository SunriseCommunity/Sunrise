using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class TokenRequest
{
    [JsonPropertyName("username")]
    [JsonRequired]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    [JsonRequired]
    public string? Password { get; set; }
}