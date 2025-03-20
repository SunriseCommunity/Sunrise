using System.Net;
using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable;

public class ErrorMessage
{
    [JsonPropertyName("status")]
    public HttpStatusCode Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}