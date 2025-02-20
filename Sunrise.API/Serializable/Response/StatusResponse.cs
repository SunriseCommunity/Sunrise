using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class StatusResponse(int usersOnline, long totalUsers, long? totalScores = null)
{
    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; } = true; // If we are here, the server is online. Makes sense, right?

    [JsonPropertyName("users_online")] public int UsersOnline { get; set; } = usersOnline;

    [JsonPropertyName("total_users")] public long TotalUsers { get; set; } = totalUsers;

    [JsonPropertyName("total_scores")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TotalScores { get; set; } = totalScores;
}