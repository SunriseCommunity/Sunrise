using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable.Performances;

public class PerformanceAttributes
{
    /**
  * The difficulty attributes.
  */
    [JsonPropertyName("difficulty")]
    public DifficultyAttributes Difficulty { get; set; }

    /**
     * Scaled miss count based on total hits.
     * 
     * Only available for osu! and osu!taiko.
     */
    [JsonPropertyName("effectiveMissCount")]
    public double? EffectiveMissCount { get; set; }

    /**
     * Upper bound on the player's tap deviation.
     * 
     * Only *optionally* available for osu!taiko.
     */
    [JsonPropertyName("estimatedUnstableRate")]
    public double? EstimatedUnstableRate { get; set; }

    /**
 * The final performance points.
 */
    [JsonPropertyName("pp")]
    public double PerformancePoints { get; set; }

    /**
     * The accuracy portion of the final pp.
     * 
     * Only available for osu! and osu!taiko.
     */
    [JsonPropertyName("ppAccuracy")]
    public double? PerformancePointsAccuracy { get; set; }

    /**
     * The aim portion of the final pp.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("ppAim")]
    public double? PerformancePointsAim { get; set; }

    /**
     * The strain portion of the final pp.
     * 
     * Only available for osu!taiko and osu!mania.
     */
    [JsonPropertyName("ppDifficulty")]
    public double? PerformancePointsDifficulty { get; set; }

    /**
     * The flashlight portion of the final pp.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("ppFlashlight")]
    public double? PerformancePointsFlashlight { get; set; }

    /**
     * The speed portion of the final pp.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("ppSpeed")]
    public double? PerformancePointsSpeed { get; set; }

    /**
     * The hitresult score state that was used for performance calculation.
     */
    [JsonPropertyName("state")]
    public ScoreState State { get; set; }
}