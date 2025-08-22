using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class PreviousUsernamesResponse
{
    public PreviousUsernamesResponse(List<string> usernames)
    {
        Usernames = usernames;
    }

    [JsonConstructor]
    public PreviousUsernamesResponse()
    {
    }

    [JsonPropertyName("usernames")]
    public List<string> Usernames { get; set; }
}