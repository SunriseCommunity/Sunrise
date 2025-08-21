using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class ChangePasswordRequest
{
    [JsonPropertyName("current_password")]
    [Required]
    public required string CurrentPassword { get; set; }

    [JsonPropertyName("new_password")]
    [Required]
    public required string NewPassword { get; set; }
}