using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class TokenRequest
{
    [JsonPropertyName("username")]
    [Required]
    public required string Username { get; set; }

    [JsonPropertyName("password")]
    [Required]
    public required string Password { get; set; }
}