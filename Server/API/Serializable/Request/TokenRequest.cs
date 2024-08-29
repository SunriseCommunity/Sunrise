using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class TokenRequest
{
    [JsonPropertyName("us")]
    public string? Username { get; set; }

    [JsonPropertyName("pa")]
    public string? Password { get; set; }
}