using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable.Events;

public class UserUsernameChanged
{
    [JsonPropertyName("NewUsername")]
    public required string NewUsername { get; set; }

    [JsonPropertyName("OldUsername")]
    public required string OldUsername { get; set; }

    [JsonPropertyName("UpdatedById")]
    public int? UpdatedById { get; set; }

    [JsonPropertyName("IsHiddenFromPreviousUsernames")]
    public bool? IsHiddenFromPreviousUsernames { get; set; }
}