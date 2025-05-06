using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditDescriptionRequest
{
    [JsonPropertyName("description")]
    public required string Description { get; set; }
}