using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditHidePreviousUsernameRequest
{
    [JsonPropertyName("event_id")]
    [Required]
    [Range(1, int.MaxValue)]
    public required int EventId { get; set; }

    [JsonPropertyName("is_hidden")]
    [Required]
    public required bool IsHidden { get; set; }
}
