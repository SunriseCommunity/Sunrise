using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Response;

[method: JsonConstructor]
public class ErrorResponse(string error)
{
    [JsonPropertyName("error")]
    public string Error { get; } = error;
}