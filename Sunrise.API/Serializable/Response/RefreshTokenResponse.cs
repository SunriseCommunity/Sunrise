using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class RefreshTokenResponse(string token, int expiresIn)
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = token;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = expiresIn;
}