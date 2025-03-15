using System.Text.Json.Serialization;

namespace Sunrise.Shared.Objects.Serializable.Performances;

public class ScoreState
{
    /**
     * Maximum combo that the score has had so far. **Not** the maximum
     * possible combo of the map so far.
     * 
     * Note that for osu!catch only fruits and droplets are considered for
     * combo.
     * 
     * Irrelevant for osu!mania.
     */
    [JsonPropertyName("maxCombo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxCombo { get; set; }

    /**
     * "Large tick" hits for osu!standard.
     * 
     * The meaning depends on the kind of score:
     * - if set on osu!stable, this field is irrelevant and can be `0`
     * - if set on osu!lazer *without* `CL`, this field is the amount of hit
     * slider ticks and repeats
     * - if set on osu!lazer *with* `CL`, this field is the amount of hit
     * slider heads, ticks, and repeats
     */
    [JsonPropertyName("osuLargeTickHits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OsuLargeTickHits { get; set; }

    /**
     * "Small tick" hits for osu!standard.
     * 
     * These are essentially the slider end hits for lazer scores without
     * slider accuracy.
     * 
     * Only relevant for osu!lazer.
     */
    [JsonPropertyName("osuSmallTickHits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OsuSmallTickHits { get; set; }

    /**
     * Amount of successfully hit slider ends.
     * 
     * Only relevant for osu!standard in lazer.
     */
    [JsonPropertyName("sliderEndHits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SliderEndHits { get; set; }

    /**
    * Amount of current gekis (n320 for osu!mania).
    */
    [JsonPropertyName("nGeki")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NGeki { get; set; }

    /**
     * Amount of current katus (tiny droplet misses for osu!catch / n200 for
     * osu!mania).
     */
    [JsonPropertyName("nKatu")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NKatu { get; set; }

    /**
    * Amount of current 300s (fruits for osu!catch).
    */
    [JsonPropertyName("n300")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? N300 { get; set; }

    /**
    * Amount of current 100s (droplets for osu!catch).
    */
    [JsonPropertyName("n100")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? N100 { get; set; }

    /**
    * Amount of current 50s (tiny droplets for osu!catch).
    */
    [JsonPropertyName("n50")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? N50 { get; set; }

    /**
    * Amount of current misses (fruits + droplets for osu!catch).
    */
    [JsonPropertyName("misses")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Misses { get; set; }
}