using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Request;

public class EditDescriptionRequest
{
    [JsonPropertyName("description")]
    [JsonRequired]
    public string? Description { get; set; }
}