using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Users;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Extensions.Beatmaps;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.Shared.Objects.Serializable;

public class Beatmap
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("beatmapset_id")]
    public int BeatmapsetId { get; set; }

    [JsonPropertyName("difficulty_rating")]
    public double DifficultyRating { get; set; }

    [JsonPropertyName("mode")]
    public string Mode { get; set; }

    [JsonPropertyName("status")]
    public string StatusString { get; set; }

    public BeatmapStatus Status => StatusString.StringToBeatmapStatus();
    public BeatmapStatusWeb StatusGeneric => StatusString.StringToBeatmapStatusSearch();

    [JsonPropertyName("total_length")]
    public int TotalLength { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("accuracy")]
    public double? Accuracy { get; set; }

    [JsonPropertyName("ar")]
    public double? AR { get; set; }

    [JsonPropertyName("bpm")]
    public double BPM { get; set; } = 0;

    [JsonPropertyName("convert")]
    public bool Convert { get; set; }

    [JsonPropertyName("count_circles")]
    public int CountCircles { get; set; }

    [JsonPropertyName("count_sliders")]
    public int CountSliders { get; set; }

    [JsonPropertyName("count_spinners")]
    public int CountSpinners { get; set; }

    [JsonPropertyName("cs")]
    public double CS { get; set; }

    [JsonPropertyName("deleted_at")]
    [JsonConverter(typeof(DateTimeUnixConverter))]
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyName("drain")]
    public double? Drain { get; set; }

    [JsonPropertyName("hit_length")]
    public int HitLength { get; set; }

    [JsonPropertyName("is_scoreable")]
    public bool IsScoreable => Status.IsScoreable();

    [JsonPropertyName("is_ranked")]
    public bool IsRanked => Status.IsRanked();

    [JsonPropertyName("last_updated")]
    [JsonConverter(typeof(DateTimeUnixConverter))]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("mode_int")]
    public int ModeInt { get; set; }

    [JsonPropertyName("passcount")]
    public int Passcount { get; set; }

    [JsonPropertyName("playcount")]
    public int Playcount { get; set; }

    [JsonPropertyName("ranked")]
    public int Ranked { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    [JsonPropertyName("failtimes")]
    public FailTimes? FailTimes { get; set; }

    [JsonPropertyName("max_combo")]
    public int? MaxCombo { get; set; }

    public User? BeatmapNominatorUser { get; set; }
}

public class FailTimes
{
    [JsonPropertyName("exit")]
    public int[]? Exit { get; set; }

    [JsonPropertyName("fail")]
    public int[]? Fail { get; set; }
}