using System.Text.Json.Serialization;
using osu.Shared;
using Sunrise.Shared.Database.Models;
using Sunrise.Shared.Extensions.Beatmaps;

namespace Sunrise.Shared.Objects.Serializable.Performances;

public class CalculateScoreRequest(Score score)
{
    [JsonPropertyName("beatmapId")]
    public int BeatmapId { get; set; } = score.BeatmapId;

    [JsonPropertyName("beatmapHash")]
    public string BeatmapHash { get; set; } = score.BeatmapHash;

    [JsonPropertyName("acc")]
    public double Accuracy { get; set; } = score.Accuracy;

    [JsonPropertyName("combo")]
    public int Combo { get; set; } = score.MaxCombo;

    [JsonPropertyName("n300")]
    public int Count300 { get; set; } = score.Count300;

    [JsonPropertyName("nGeki")]
    public int CountGeki { get; set; } = score.CountGeki;

    [JsonPropertyName("n100")]
    public int Count100 { get; set; } = score.Count100;

    [JsonPropertyName("nKatu")]
    public int CountKatu { get; set; } = score.CountKatu;

    [JsonPropertyName("n50")]
    public int Count50 { get; set; } = score.Count50;

    [JsonPropertyName("misses")]
    public int CountMiss { get; set; } = score.CountMiss;

    [JsonPropertyName("mode")]
    public GameMode GameMode { get; set; } = score.GameMode.ToVanillaGameMode();

    [JsonPropertyName("mods")]
    public Mods Mods { get; set; } = score.Mods;

    [JsonPropertyName("isScoreFailed")]
    public bool IsScoreFailed { get; set; } = !score.IsPassed;
}