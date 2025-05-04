using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; set; }
}