using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class StatusResponse(int usersOnline, long totalUsers, long? totalScores = null, long? totalRestrictions = null, List<UserResponse> usersOnlineData = null, List<UserResponse> usersRegisteredData = null)
{
    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; } = true; // If we are here, the server is online. Makes sense, right?

    [JsonPropertyName("users_online")]
    public int UsersOnline { get; set; } = usersOnline;

    [JsonPropertyName("current_users_online")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UserResponse>? CurrentUsersOnline { get; set; } = usersOnlineData;

    [JsonPropertyName("total_users")]
    public long TotalUsers { get; set; } = totalUsers;

    [JsonPropertyName("recent_users")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UserResponse>? RecentUsers { get; set; } = usersRegisteredData;

    [JsonPropertyName("total_scores")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TotalScores { get; set; } = totalScores;

    [JsonPropertyName("total_restrictions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TotalRestrictions { get; set; } = totalRestrictions;
}