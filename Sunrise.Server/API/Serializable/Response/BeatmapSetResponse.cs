using System.Text.Json.Serialization;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Serializable.Response;

public class BeatmapSetResponse(BaseSession session, BeatmapSet beatmapSet)
{
    [JsonPropertyName("id")]
    public int Id { get; set; } = beatmapSet.Id;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = beatmapSet.Artist;

    [JsonPropertyName("title")]
    public string Title { get; set; } = beatmapSet.Title;

    [JsonPropertyName("creator")]
    public string Creator { get; set; } = beatmapSet.Creator;

    [JsonPropertyName("creator_id")]
    public int CreatorId { get; set; } = beatmapSet.UserId;

    [JsonPropertyName("status")]
    public string StatusString { get; set; } = beatmapSet.StatusString;

    [JsonPropertyName("last_updated")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime LastUpdated { get; set; } = beatmapSet.LastUpdated;

    [JsonPropertyName("submitted_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime SubmittedDate { get; set; } = beatmapSet.SubmittedDate;

    [JsonPropertyName("ranked_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? RankedDate { get; set; } = beatmapSet.RankedDate;

    [JsonPropertyName("video")]
    public bool HasVideo { get; set; } = beatmapSet.HasVideo;

    [JsonPropertyName("beatmaps")]
    public List<BeatmapResponse> Beatmaps { get; set; } =
        beatmapSet.Beatmaps.Select(beatmap => new BeatmapResponse(session, beatmap, beatmapSet)).ToList();

    [JsonPropertyName("description")]
    public string Description { get; set; } = beatmapSet.Description?.description ?? "";

    [JsonPropertyName("genre")]
    public string Genre { get; set; } = beatmapSet.Genre?.Name ?? "Unknown";

    [JsonPropertyName("language")]
    public string Language { get; set; } = beatmapSet.Language?.Name ?? "Unknown";

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = beatmapSet.Tags.Split(' ');
}