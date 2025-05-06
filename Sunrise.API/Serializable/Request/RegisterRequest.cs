using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class RegisterRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [JsonPropertyName("email")]
    public required string Email { get; set; }
}