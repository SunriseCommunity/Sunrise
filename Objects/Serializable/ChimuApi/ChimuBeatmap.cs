using System.Text.Json.Serialization;

namespace Sunrise.Objects.Serializable.ChimuApi;

public class ChimuBeatmap
{
    [JsonPropertyName("BeatmapId")]
    public int BeatmapId { get; set; }

    [JsonPropertyName("ParentSetId")]
    public int ParentSetId { get; set; }

    [JsonPropertyName("DiffName")]
    public string DiffName { get; set; }

    [JsonPropertyName("FileMD5")]
    public string FileMD5 { get; set; }

    [JsonPropertyName("Mode")]
    public int Mode { get; set; }

    [JsonPropertyName("BPM")]
    public double BPM { get; set; }

    [JsonPropertyName("AR")]
    public double AR { get; set; }

    [JsonPropertyName("OD")]
    public double OD { get; set; }

    [JsonPropertyName("CS")]
    public double CS { get; set; }

    [JsonPropertyName("HP")]
    public double HP { get; set; }

    [JsonPropertyName("TotalLength")]
    public int TotalLength { get; set; }

    [JsonPropertyName("HitLength")]
    public int HitLength { get; set; }

    [JsonPropertyName("Playcount")]
    public int Playcount { get; set; }

    [JsonPropertyName("Passcount")]
    public int Passcount { get; set; }

    [JsonPropertyName("MaxCombo")]
    public int MaxCombo { get; set; }

    [JsonPropertyName("DifficultyRating")]
    public double DifficultyRating { get; set; }

    [JsonPropertyName("OsuFile")]
    public string OsuFile { get; set; }

    [JsonPropertyName("DownloadPath")]
    public string DownloadPath { get; set; }
}