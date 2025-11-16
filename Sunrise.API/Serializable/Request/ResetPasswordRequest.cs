using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class ResetPasswordRequest
{
    [JsonPropertyName("new_password")]
    [Required]
    public required string NewPassword { get; set; }
}