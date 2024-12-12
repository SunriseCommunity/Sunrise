using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class ResetPassRequest
{
    [JsonPropertyName("old_password")]
    [JsonRequired]
    public string? OldPassword { get; set; }

    [JsonPropertyName("new_password")]
    [JsonRequired]
    public string? NewPassword { get; set; } 
}