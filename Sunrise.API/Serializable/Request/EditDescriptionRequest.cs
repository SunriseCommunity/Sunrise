using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditDescriptionRequest
{
    [JsonPropertyName("description")]
    [Required]
    [MaxLength(2000, ErrorMessage = "Description cannot be longer than 2000 characters.")]
    public required string Description { get; set; }
}