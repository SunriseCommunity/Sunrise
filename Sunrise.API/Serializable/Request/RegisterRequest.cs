using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class RegisterRequest
{
    [JsonPropertyName("username")]
    [Required]
    public required string Username { get; set; }

    [JsonPropertyName("password")]
    [Required]
    public required string Password { get; set; }

    [JsonPropertyName("email")]
    [Required]
    [RegularExpression("^\\S+@\\S+\\.\\S+$", ErrorMessage = "Invalid email format")]
    public required string Email { get; set; }
}