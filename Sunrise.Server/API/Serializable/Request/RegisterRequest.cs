using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class RegisterRequest
{
    [JsonPropertyName("username")]
    [JsonRequired]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    [JsonRequired]
    public string? Password { get; set; }

    [JsonPropertyName("email")]
    [JsonRequired]
    public string? Email { get; set; }
}