using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class TokenResponse(string token, string refreshToken, int expiresIn)
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = token;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = refreshToken;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; } = expiresIn;
}