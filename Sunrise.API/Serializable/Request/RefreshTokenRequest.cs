using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    [JsonRequired]
    public string? RefreshToken { get; set; }
}