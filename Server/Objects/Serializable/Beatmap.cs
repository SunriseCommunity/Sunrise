using System.Text.Json.Serialization;
using Sunrise.Server.Types.Enums;

namespace Sunrise.Server.Objects.Serializable;

public class Beatmap
{
    private readonly Dictionary<string, BeatmapStatus> _statusMap = new()
    {
        ["loved"] = BeatmapStatus.Loved,
        ["qualified"] = BeatmapStatus.Qualified,
        ["approved"] = BeatmapStatus.Approved,
        ["ranked"] = BeatmapStatus.Ranked,
        ["pending"] = BeatmapStatus.Pending,
        ["graveyard"] = BeatmapStatus.Pending
    };

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

    public BeatmapStatus Status => _statusMap[StatusString ?? "graveyard"];

    [JsonPropertyName("total_length")]
    public int TotalLength { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("accuracy")]
    public double Accuracy { get; set; }

    [JsonPropertyName("ar")]
    public double AR { get; set; }

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
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyName("drain")]
    public double Drain { get; set; }

    [JsonPropertyName("hit_length")]
    public int HitLength { get; set; }

    [JsonPropertyName("is_scoreable")]
    public bool IsScoreable { get; set; }

    [JsonPropertyName("last_updated")]
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
    public string Checksum { get; set; }

    [JsonPropertyName("max_combo")]
    public int? MaxCombo { get; set; }
}