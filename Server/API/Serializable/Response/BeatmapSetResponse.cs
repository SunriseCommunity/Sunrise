using System.Text.Json.Serialization;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.API.Serializable.Response;

public class BeatmapSetResponse(BeatmapSet beatmapSet)
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
    public DateTime LastUpdated { get; set; } = beatmapSet.LastUpdated;

    [JsonPropertyName("ranked_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? RankedDate { get; set; } = beatmapSet.RankedDate;

    [JsonPropertyName("video")]
    public bool HasVideo { get; set; } = beatmapSet.HasVideo;

    [JsonPropertyName("beatmaps")]
    public List<BeatmapResponse> Beatmaps { get; set; } =
        beatmapSet.Beatmaps.Select(beatmap => new BeatmapResponse(beatmap)).ToList();
}