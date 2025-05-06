using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class UserWithStats(UserResponse user, UserStatsResponse stats)
{
    [JsonPropertyName("user")]
    public UserResponse User { get; set; } = user;

    [JsonPropertyName("stats")]
    public UserStatsResponse Stats { get; set; } = stats;
}

public class LeaderboardResponse
{
    public LeaderboardResponse(List<UserWithStats> data, int totalCount)
    {
        Users = data.ToList();
        TotalCount = totalCount;
    }

    [JsonConstructor]
    public LeaderboardResponse() { }

    [JsonPropertyName("users")]
    public List<UserWithStats> Users { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}