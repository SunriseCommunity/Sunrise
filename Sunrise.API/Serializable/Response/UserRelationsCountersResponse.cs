using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class UserRelationsCountersResponse(int followers, int following)
{
    [JsonPropertyName("followers")]
    public int Followers { get; set; } = followers;

    [JsonPropertyName("following")]
    public int Following { get; set; } = following;
}