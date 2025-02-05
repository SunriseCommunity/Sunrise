using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Response;

public class ErrorResponse(string message)
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = message;
}