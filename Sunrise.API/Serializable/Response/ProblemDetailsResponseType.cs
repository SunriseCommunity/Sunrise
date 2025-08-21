using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Sunrise.API.Serializable.Response;

/// <summary>
/// Class only to properly show our ProblemDetails with OpenAPI
/// </summary>
public class ProblemDetailsResponseType : ProblemDetails
{

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("traceId")]
    public string? RequestId { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("errors")]
    public object? Errors { get; set; }
    
    [JsonIgnore]
    public new IDictionary<string, object?> Extensions { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}