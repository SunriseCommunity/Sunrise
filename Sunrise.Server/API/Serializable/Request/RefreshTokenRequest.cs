using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    [JsonRequired]
    public string? RefreshToken { get; set; }
}