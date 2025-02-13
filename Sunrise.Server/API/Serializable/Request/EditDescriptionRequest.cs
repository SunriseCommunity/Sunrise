using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class EditDescriptionRequest
{
    [JsonPropertyName("description")]
    [JsonRequired]
    public string? Description { get; set; }
}