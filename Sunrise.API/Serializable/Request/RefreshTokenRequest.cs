using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class RefreshTokenRequest
{
    [JsonPropertyName("refresh_token")]
    [Required]
    public required string RefreshToken { get; set; }
}