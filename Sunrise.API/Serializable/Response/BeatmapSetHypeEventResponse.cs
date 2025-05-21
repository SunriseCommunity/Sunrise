using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Events;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Enums.Users;
using Sunrise.Shared.Objects.Serializable.Events;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class BeatmapEventResponse(SessionRepository sessionRepository, EventBeatmap eventBeatmap, BeatmapSetResponse beatmapSet)
{
    [JsonPropertyName("event_id")]
    public int EventId { get; set; } = eventBeatmap.Id;

    [JsonPropertyName("executor")]
    public UserResponse Executor { get; set; } = new(sessionRepository, eventBeatmap.Executor);

    [JsonPropertyName("type")]
    public BeatmapEventType BeatmapEventType { get; set; } = eventBeatmap.EventType;

    [JsonPropertyName("beatmapset_id")]
    public int BeatmapSetId { get; set; } = eventBeatmap.BeatmapSetId;

    [JsonPropertyName("beatmapset")]
    public BeatmapSetResponse BeatmapSet { get; set; } = beatmapSet;

    [JsonPropertyName("beatmap_hash")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BeatmapHash { get; set; } = !string.IsNullOrEmpty(eventBeatmap.JsonData) && eventBeatmap.EventType == BeatmapEventType.BeatmapStatusChanged ? eventBeatmap.GetData<BeatmapStatusChanged>()?.BeatmapHash : null;

    [JsonPropertyName("new_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BeatmapStatusWeb? NewStatus { get; set; } = !string.IsNullOrEmpty(eventBeatmap.JsonData) && eventBeatmap.EventType == BeatmapEventType.BeatmapStatusChanged ? eventBeatmap.GetData<BeatmapStatusChanged>()?.NewStatus : null;

    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime Date { get; set; } = eventBeatmap.Time;
}