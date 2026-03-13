using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditIgnoreLoginDataRequest
{
    [JsonPropertyName("is_ignored")]
    [Required]
    public required bool IsIgnored { get; set; }
}
