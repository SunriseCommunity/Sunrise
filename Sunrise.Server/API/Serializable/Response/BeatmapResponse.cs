using System.Text.Json.Serialization;
using Sunrise.Server.Objects;
using Sunrise.Server.Objects.Serializable;

namespace Sunrise.Server.API.Serializable.Response;

public class BeatmapResponse(BaseSession session, Beatmap beatmap, BeatmapSet? beatmapSet = null)
{
    [JsonPropertyName("id")]
    public int Id { get; set; } = beatmap.Id;

    [JsonPropertyName("beatmapset_id")]
    public int BeatmapsetId { get; set; } = beatmap.BeatmapsetId;

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = beatmap.Checksum;

    [JsonPropertyName("version")]
    public string Version { get; set; } = beatmap.Version;

    [JsonPropertyName("status")]
    public string Status { get; set; } = beatmap.StatusString;

    [JsonPropertyName("star_rating_osu")]
    public double StarRating { get; set; } = beatmap.ModeInt switch
    {
        0 => beatmap.DifficultyRating,
        _ => 0
    };

    [JsonPropertyName("star_rating_taiko")]
    public double StarRatingTaiko { get; set; } = beatmap.ModeInt switch
    {
        1 => beatmap.DifficultyRating,
        0 => beatmapSet?.ConvertedBeatmaps.FirstOrDefault(b => b.ModeInt == 1 && b.Id == beatmap.Id)?.DifficultyRating ?? 0,
        _ => 0
    };

    [JsonPropertyName("star_rating_ctb")]
    public double StarRatingCatch { get; set; } = beatmap.ModeInt switch
    {
        2 => beatmap.DifficultyRating,
        0 => beatmapSet?.ConvertedBeatmaps.FirstOrDefault(b => b.ModeInt == 2 && b.Id == beatmap.Id)?.DifficultyRating ?? 0,
        _ => 0
    };

    [JsonPropertyName("star_rating_mania")]
    public double StarRatingMania { get; set; } = beatmap.ModeInt switch
    {
        3 => beatmap.DifficultyRating,
        0 => beatmapSet?.ConvertedBeatmaps.FirstOrDefault(b => b.ModeInt == 3 && b.Id == beatmap.Id)?.DifficultyRating ?? 0,
        _ => 0
    };

    [JsonPropertyName("total_length")]
    public int TotalLength { get; set; } = beatmap.TotalLength;

    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; } = beatmap.MaxCombo ?? 0;

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; } = beatmap.Accuracy;

    [JsonPropertyName("ar")]
    public double AR { get; set; } = beatmap.AR;

    [JsonPropertyName("bpm")]
    public double BPM { get; set; } = beatmap.BPM;

    [JsonPropertyName("convert")]
    public bool Convert { get; set; } = beatmap.Convert;

    [JsonPropertyName("count_circles")]
    public int CountCircles { get; set; } = beatmap.CountCircles;

    [JsonPropertyName("count_sliders")]
    public int CountSliders { get; set; } = beatmap.CountSliders;

    [JsonPropertyName("count_spinners")]
    public int CountSpinners { get; set; } = beatmap.CountSpinners;

    [JsonPropertyName("cs")]
    public double CS { get; set; } = beatmap.CS;

    [JsonPropertyName("deleted_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? DeletedAt { get; set; } = beatmap.DeletedAt;

    [JsonPropertyName("drain")]
    public double Drain { get; set; } = beatmap.Drain;

    [JsonPropertyName("hit_length")]
    public int HitLength { get; set; } = beatmap.HitLength;

    [JsonPropertyName("is_scoreable")]
    public bool IsScoreable { get; set; } = beatmap.IsScoreable;

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; } = beatmap.LastUpdated;

    [JsonPropertyName("mode_int")]
    public int ModeInt { get; set; } = beatmap.ModeInt;

    [JsonPropertyName("ranked")]
    public int Ranked { get; set; } = beatmap.Ranked;

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; } = beatmapSet?.Title;

    [JsonPropertyName("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Artist { get; set; } = beatmapSet?.Artist;

    [JsonPropertyName("creator")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Creator { get; set; } = beatmapSet?.RelatedUsers?.FirstOrDefault(u => u.Id == beatmap.UserId)?.Username ?? "Unknown";

    [JsonPropertyName("creator_id")]
    public int CreatorId { get; set; } = beatmap.UserId;

    // TODO: Add playcount and favourite count
}