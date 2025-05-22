using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class BeatmapEventResponse
{
    [JsonConstructor]
    public BeatmapEventResponse() { }

    public BeatmapEventResponse(SessionRepository sessionRepository, EventBeatmap eventBeatmap, BeatmapSetResponse beatmapSet)
    {
        EventId = eventBeatmap.Id;
        Executor = new UserResponse(sessionRepository, eventBeatmap.Executor);
        BeatmapEventType = eventBeatmap.EventType;
        BeatmapSetId = beatmapSet.Id;
        BeatmapSet = beatmapSet;
        BeatmapHash = !string.IsNullOrEmpty(eventBeatmap.JsonData) && eventBeatmap.EventType == BeatmapEventType.BeatmapStatusChanged ? eventBeatmap.GetData<BeatmapStatusChanged>()?.BeatmapHash : null;
        NewStatus = !string.IsNullOrEmpty(eventBeatmap.JsonData) && eventBeatmap.EventType == BeatmapEventType.BeatmapStatusChanged ? eventBeatmap.GetData<BeatmapStatusChanged>()?.NewStatus : null;
        CreatedAt = eventBeatmap.Time;
    }

    [JsonPropertyName("event_id")]
    public int EventId { get; set; }

    [JsonPropertyName("executor")]
    public UserResponse Executor { get; set; }

    [JsonPropertyName("type")]
    public BeatmapEventType BeatmapEventType { get; set; }

    [JsonPropertyName("beatmapset_id")]
    public int BeatmapSetId { get; set; }

    [JsonPropertyName("beatmapset")]
    public BeatmapSetResponse BeatmapSet { get; set; }

    [JsonPropertyName("beatmap_hash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BeatmapHash { get; set; }

    [JsonPropertyName("new_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BeatmapStatusWeb? NewStatus { get; set; }

    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime CreatedAt { get; set; }
}