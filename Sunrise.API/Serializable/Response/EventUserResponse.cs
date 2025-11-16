using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class EventUserResponse
{
    [JsonConstructor]
    public EventUserResponse()
    {
    }

    public EventUserResponse(SessionRepository sessionRepository, EventUser eventUser)
    {
        Id = eventUser.Id;
        User = new UserResponse(sessionRepository, eventUser.User);
        EventType = eventUser.EventType;
        Ip = eventUser.Ip;
        JsonData = eventUser.JsonData;
        Time = eventUser.Time;
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("user")]
    public UserResponse User { get; set; }

    [JsonPropertyName("event_type")]
    public UserEventType EventType { get; set; }

    [JsonPropertyName("ip")]
    public string Ip { get; set; }

    [JsonPropertyName("json_data")]
    public string JsonData { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime Time { get; set; }
}