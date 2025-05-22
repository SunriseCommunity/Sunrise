using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;
using Sunrise.Shared.Utils.Converters;

namespace Sunrise.API.Serializable.Response;

public class BeatmapResponse
{
    public BeatmapResponse(SessionRepository sessions, Beatmap beatmap, BeatmapSet? beatmapSet = null)
    {
        Id = beatmap.Id;
        BeatmapsetId = beatmap.BeatmapsetId;
        Hash = beatmap.Checksum;
        Version = beatmap.Version;
        Status = beatmap.StatusGeneric;
        StarRating = beatmap.ModeInt switch
        {
            0 => beatmap.DifficultyRating,
            _ => 0
        };
        StarRatingTaiko = beatmap.ModeInt switch
        {
            1 => beatmap.DifficultyRating,
            0 => beatmapSet?.ConvertedBeatmaps.FirstOrDefault(b => b.ModeInt == 1 && b.Id == beatmap.Id)?.DifficultyRating ?? 0,
            _ => 0
        };
        StarRatingCatch = beatmap.ModeInt switch
        {
            2 => beatmap.DifficultyRating,
            0 => beatmapSet?.ConvertedBeatmaps.FirstOrDefault(b => b.ModeInt == 2 && b.Id == beatmap.Id)?.DifficultyRating ?? 0,
            _ => 0
        };
        StarRatingMania = beatmap.ModeInt switch
        {
            3 => beatmap.DifficultyRating,
            0 => beatmapSet?.ConvertedBeatmaps.FirstOrDefault(b => b.ModeInt == 3 && b.Id == beatmap.Id)?.DifficultyRating ?? 0,
            _ => 0
        };
        TotalLength = beatmap.TotalLength;
        MaxCombo = beatmap.MaxCombo ?? 0;
        Accuracy = beatmap.Accuracy;
        AR = beatmap.AR;
        BPM = beatmap.BPM;
        Convert = beatmap.Convert;
        CountCircles = beatmap.CountCircles;
        CountSliders = beatmap.CountSliders;
        CountSpinners = beatmap.CountSpinners;
        CS = beatmap.CS;
        DeletedAt = beatmap.DeletedAt;
        Drain = beatmap.Drain;
        HitLength = beatmap.HitLength;
        IsScoreable = beatmap.IsScoreable;
        IsRanked = beatmap.IsRanked;
        LastUpdated = beatmap.LastUpdated;
        ModeInt = beatmap.ModeInt;
        Mode = (GameMode)beatmap.ModeInt;
        Ranked = beatmap.Ranked;
        Title = beatmapSet?.Title;
        Artist = beatmapSet?.Artist;
        Creator = beatmapSet?.RelatedUsers?.FirstOrDefault(u => u.Id == beatmap.UserId)?.Username ?? "Unknown";
        CreatorId = beatmap.UserId;
        BeatmapNominatorUser = beatmap.BeatmapNominatorUser != null ? new UserResponse(sessions, beatmap.BeatmapNominatorUser) : null;
    }

    [JsonConstructor]
    public BeatmapResponse()
    {
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("beatmapset_id")]
    public int BeatmapsetId { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("status")]
    public BeatmapStatusWeb Status { get; set; }

    [JsonPropertyName("star_rating_osu")]
    public double StarRating { get; set; }

    [JsonPropertyName("star_rating_taiko")]
    public double StarRatingTaiko { get; set; }

    [JsonPropertyName("star_rating_ctb")]
    public double StarRatingCatch { get; set; }

    [JsonPropertyName("star_rating_mania")]
    public double StarRatingMania { get; set; }

    [JsonPropertyName("total_length")]
    public int TotalLength { get; set; }

    [JsonPropertyName("max_combo")]
    public int MaxCombo { get; set; }

    [JsonPropertyName("accuracy")]
    public double? Accuracy { get; set; }

    [JsonPropertyName("ar")]
    public double? AR { get; set; }

    [JsonPropertyName("bpm")]
    public double BPM { get; set; }

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
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyName("drain")]
    public double? Drain { get; set; }

    [JsonPropertyName("hit_length")]
    public int HitLength { get; set; }

    [JsonPropertyName("is_scoreable")]
    public bool IsScoreable { get; set; }

    [JsonPropertyName("is_ranked")]
    public bool IsRanked { get; set; }

    [JsonPropertyName("last_updated")]
    [JsonConverter(typeof(DateTimeWithTimezoneConverter))]
    public DateTime LastUpdated { get; set; }

    [JsonPropertyName("mode_int")]
    public int ModeInt { get; set; }

    [JsonPropertyName("mode")]
    public GameMode Mode { get; set; }

    [JsonPropertyName("ranked")]
    public int Ranked { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("artist")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Artist { get; set; }

    [JsonPropertyName("creator")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Creator { get; set; }

    [JsonPropertyName("creator_id")]
    public int CreatorId { get; set; }

    [JsonPropertyName("beatmap_nominator_user")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserResponse? BeatmapNominatorUser { get; set; }

    // TODO: Add playcount and favourite count
}