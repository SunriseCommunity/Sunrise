using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Response;

public class UserWithStats(UserResponse user, UserStatsResponse stats)
{
    [JsonPropertyName("user")]
    public UserResponse User { get; set; } = user;

    [JsonPropertyName("stats")]
    public UserStatsResponse Stats { get; set; } = stats;
}

public class LeaderboardResponse(List<UserWithStats?> data, int totalCount)
{
    [JsonPropertyName("users")]
    public List<UserWithStats?> Users { get; set; } = data.ToList();

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}