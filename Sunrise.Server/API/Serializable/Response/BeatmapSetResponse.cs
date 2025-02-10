using System.Text.Json.Serialization;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;
using Sunrise.Server.Utils;

namespace Sunrise.Server.API.Serializable.Response;

public class BeatmapSetResponse
{
    public BeatmapSetResponse(BaseSession session, BeatmapSet beatmapSet)
    {
        Id = beatmapSet.Id;
        Artist = beatmapSet.Artist;
        Title = beatmapSet.Title;
        Creator = beatmapSet.Creator;
        CreatorId = beatmapSet.UserId;
        StatusString = beatmapSet.StatusString;
        LastUpdated = beatmapSet.LastUpdated;
        SubmittedDate = beatmapSet.SubmittedDate;
        RankedDate = beatmapSet.RankedDate;
        HasVideo = beatmapSet.HasVideo;
        Beatmaps = beatmapSet.Beatmaps.Select(beatmap => new BeatmapResponse(session, beatmap, beatmapSet)).ToList();
        Description = beatmapSet.Description?.description ?? "";
        Genre = beatmapSet.Genre?.Name ?? "Unknown";
        Language = beatmapSet.Language?.Name ?? "Unknown";
        Tags = beatmapSet.Tags.Split(' ');
    }

    [JsonConstructor]
    public BeatmapSetResponse()
    {
    }


    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("artist")]
    public string Artist { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("creator")]
    public string Creator { get; set; }

    [JsonPropertyName("creator_id")]
    public int CreatorId { get; set; }

    [JsonPropertyName("status")]
    public string StatusString { get; set; }

    [JsonPropertyName("last_updated")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("submitted_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime SubmittedDate { get; set; }

    [JsonPropertyName("ranked_date")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? RankedDate { get; set; }

    [JsonPropertyName("video")]
    public bool HasVideo { get; set; }

    [JsonPropertyName("beatmaps")]
    public List<BeatmapResponse> Beatmaps { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("genre")]
    public string Genre { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; }
}