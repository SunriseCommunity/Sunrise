using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class StatusResponse
{
    public StatusResponse(int usersOnline, long totalUsers, long? totalScores = null, long? totalRestrictions = null, List<UserResponse> usersOnlineData = null, List<UserResponse> usersRegisteredData = null)
    {
        IsOnline = true;
        UsersOnline = usersOnline;
        CurrentUsersOnline = usersOnlineData;
        TotalUsers = totalUsers;
        RecentUsers = usersRegisteredData;
        TotalScores = totalScores;
        TotalRestrictions = totalRestrictions;

    }

    [JsonConstructor]
    public StatusResponse()
    {
    }

    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("users_online")]
    public int UsersOnline { get; set; }

    [JsonPropertyName("current_users_online")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UserResponse>? CurrentUsersOnline { get; set; }

    [JsonPropertyName("total_users")]
    public long TotalUsers { get; set; }

    [JsonPropertyName("recent_users")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<UserResponse>? RecentUsers { get; set; }

    [JsonPropertyName("total_scores")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TotalScores { get; set; }

    [JsonPropertyName("total_restrictions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TotalRestrictions { get; set; }
}