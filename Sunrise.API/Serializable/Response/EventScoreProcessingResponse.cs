using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class EventScoreProcessingResponse
{
    [JsonConstructor]
    public EventScoreProcessingResponse()
    {
    }

    public EventScoreProcessingResponse(SessionRepository sessionRepository, EventScoreProcessing scoreProcessingEvent)
    {
        Id = scoreProcessingEvent.Id;
        EventType = scoreProcessingEvent.EventType;
        Executor = scoreProcessingEvent.Executor != null ? new UserResponse(sessionRepository, scoreProcessingEvent.Executor) : null;
        ScoreId = scoreProcessingEvent.ScoreId;
        TaskId = scoreProcessingEvent.TaskId;
        JsonData = scoreProcessingEvent.JsonData;
        Time = scoreProcessingEvent.Time;
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("event_type")]
    public ScoreProcessingEventType EventType { get; set; }

    [JsonPropertyName("executor")]
    public UserResponse? Executor { get; set; }

    [JsonPropertyName("score_id")]
    public int? ScoreId { get; set; }

    [JsonPropertyName("task_id")]
    public int? TaskId { get; set; }

    [JsonPropertyName("json_data")]
    public string? JsonData { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime Time { get; set; }
}