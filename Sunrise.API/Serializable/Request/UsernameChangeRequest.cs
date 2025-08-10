using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class UsernameChangeRequest
{
    [JsonPropertyName("new_username")]
    [Required]
    public required string NewUsername { get; set; }
}