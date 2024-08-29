using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}