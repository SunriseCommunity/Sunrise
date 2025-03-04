using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class UserWithStatsResponse(UserResponse user, UserStatsResponse? stats = null)
{
    [JsonPropertyName("user")]
    public UserResponse User { get; set; } = user;

    [JsonPropertyName("stats")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserStatsResponse? Stats { get; set; } = stats;
}