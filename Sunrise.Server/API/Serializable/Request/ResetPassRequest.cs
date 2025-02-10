using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Request;

public class ChangePasswordRequest
{
    [JsonPropertyName("current_password")]
    [JsonRequired]
    public string? CurrentPassword { get; set; }

    [JsonPropertyName("new_password")]
    [JsonRequired]
    public string? NewPassword { get; set; }
}