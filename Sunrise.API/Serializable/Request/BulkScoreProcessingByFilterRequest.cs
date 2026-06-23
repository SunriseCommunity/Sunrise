using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using osu.Shared;
using Sunrise.Shared.Enums.Scores;
using BeatmapStatus = Sunrise.Shared.Enums.Beatmaps.BeatmapStatus;
using GameMode = Sunrise.Shared.Enums.Beatmaps.GameMode;
using SubmissionStatus = Sunrise.Shared.Enums.Scores.SubmissionStatus;

namespace Sunrise.API.Serializable.Request;

public class BulkScoreProcessingByFilterRequest
{
    [JsonPropertyName("action")]
    [Required]
    public ScoreTaskType Action { get; set; }

    [JsonPropertyName("user_id")]
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [JsonPropertyName("mode")]
    public GameMode? Mode { get; set; }

    [JsonPropertyName("mods")]
    public IEnumerable<Mods>? Mods { get; set; }

    [JsonPropertyName("submission_status")]
    public SubmissionStatus? SubmissionStatus { get; set; }

    [JsonPropertyName("beatmap_status")]
    public BeatmapStatus? BeatmapStatus { get; set; }

    [JsonPropertyName("submitted_from")]
    public DateTime? SubmittedFrom { get; set; }

    [JsonPropertyName("submitted_to")]
    public DateTime? SubmittedTo { get; set; }
}