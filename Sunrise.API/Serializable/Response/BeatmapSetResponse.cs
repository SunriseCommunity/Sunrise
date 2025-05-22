using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class BeatmapSetResponse
{
    public BeatmapSetResponse(SessionRepository sessions, BeatmapSet beatmapSet)
    {
        Id = beatmapSet.Id;
        Artist = beatmapSet.Artist;
        Title = beatmapSet.Title;
        Creator = beatmapSet.Creator;
        CreatorId = beatmapSet.UserId;
        Status = beatmapSet.StatusGeneric;
        LastUpdated = beatmapSet.LastUpdated;
        SubmittedDate = beatmapSet.SubmittedDate;
        RankedDate = beatmapSet.RankedDate;
        HasVideo = beatmapSet.HasVideo;
        Beatmaps = beatmapSet.Beatmaps.Select(beatmap => new BeatmapResponse(sessions, beatmap, beatmapSet)).ToList();
        Description = beatmapSet.Description?.description ?? "";
        Genre = beatmapSet.Genre?.Name ?? "Unknown";
        Language = beatmapSet.Language?.Name ?? "Unknown";
        Tags = beatmapSet.Tags.Split(' ');
        BeatmapNominatorUser = beatmapSet.BeatmapNominatorUser != null ? new UserResponse(sessions, beatmapSet.BeatmapNominatorUser) : null;
        CanBeHyped = beatmapSet.CanBeHyped;
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
    public BeatmapStatusWeb Status { get; set; }

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

    [JsonPropertyName("beatmap_nominator_user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserResponse? BeatmapNominatorUser { get; set; }

    [JsonPropertyName("can_be_hyped")]
    public bool CanBeHyped { get; set; }
}