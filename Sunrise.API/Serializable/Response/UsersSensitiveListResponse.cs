using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class UsersSensitiveListResponse
{
    public UsersSensitiveListResponse(List<UserSensitiveResponse> users, int totalCount)
    {
        Users = users;
        TotalCount = totalCount;
    }

    [JsonConstructor]
    public UsersSensitiveListResponse()
    {
    }

    [JsonPropertyName("users")]
    public List<UserSensitiveResponse> Users { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }
}
