using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Objects.Serializable.Performances;

public class DifficultyAttributes
{
    /**
     * The difficulty of the aim skill.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("aim")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Aim { get; set; }

    /**
     * Weighted sum of aim strains.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("aimDifficultStrainCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AimDifficultStrainCount { get; set; }

    /**
     * The approach rate.
     * 
     * Only available for osu! and osu!catch.
     */
    [JsonPropertyName("ar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? AR { get; set; }

    /**
     * The difficulty of the color skill.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Color { get; set; }

    /**
     * The difficulty of the flashlight skill.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("flashlight")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Flashlight { get; set; }

    /**
     * The perceived hit window for an n300 inclusive of rate-adjusting mods (DT/HT/etc)
     * 
     * Only available for osu!taiko and osu!mania.
     */
    [JsonPropertyName("greatHitWindow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? GreatHitWindow { get; set; }

    /**
     * The health drain rate.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("hp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? HP { get; set; }

    /**
     * Whether the map was a convert i.e. an osu! map.
     */
    [JsonPropertyName("isConvert")]
    public bool IsConvert { get; set; }

    /**
     * Return the maximum combo.
     */
    [JsonPropertyName("maxCombo")]
    public int MaxCombo { get; set; }

    /**
     * The attributes' gamemode.
     */
    [JsonPropertyName("mode")]
    public GameMode Mode { get; set; }

    /**
     * The ratio of stamina difficulty from mono-color (single color) streams to total stamina difficulty.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("monoStaminaFactor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MonoStaminaFactor { get; set; }

    /**
     * The amount of circles.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nCircles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NCircles { get; set; }

    /**
     * The amount of droplets.
     * 
     * Only available for osu!catch.
     */
    [JsonPropertyName("nDroplets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NDroplets { get; set; }

    /**
     * The amount of fruits.
     * 
     * Only available for osu!catch.
     */
    [JsonPropertyName("nFruits")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NFruits { get; set; }

    /**
     * The amount of hold notes in the map.
     * 
     * Only available for osu!mania.
     */
    [JsonPropertyName("nHoldNotes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NHoldNotes { get; set; }

    /**
     * The amount of "large ticks".
     * 
     * The meaning depends on the kind of score:
     * - if set on osu!stable, this value is irrelevant
     * - if set on osu!lazer *without* `CL`, this value is the amount of slider ticks and repeats
     * - if set on osu!lazer *with* `CL`, this value is the amount of slider heads, ticks, and repeats
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nLargeTicks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NLargeTicks { get; set; }

    /**
     * The amount of hitobjects in the map.
     * 
     * Only available for osu!mania.
     */
    [JsonPropertyName("nObjects")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NObjects { get; set; }

    /**
     * The amount of sliders.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nSliders")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NSliders { get; set; }

    /**
     * The amount of spinners.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nSpinners")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NSpinners { get; set; }

    /**
     * The amount of tiny droplets.
     * 
     * Only available for osu!catch.
     */
    [JsonPropertyName("nTinyDroplets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NTinyDroplets { get; set; }

    /**
     * The overall difficulty.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("od")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OD { get; set; }

    /**
     * The perceived hit window for an n100 inclusive of rate-adjusting mods (DT/HT/etc).
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("okHitWindow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? OkHitWindow { get; set; }

    /**
     * The difficulty of the hardest parts of the map.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("peak")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Peak { get; set; }

    /**
     * The difficulty of the rhythm skill.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("rhythm")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Rhythm { get; set; }

    /**
     * The ratio of the aim strain with and without considering sliders
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("sliderFactor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SliderFactor { get; set; }


    /**
     * The difficulty of the speed skill.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("speed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Speed { get; set; }

    /**
     * Weighted sum of speed strains.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("speedDifficultStrainCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SpeedDifficultStrainCount { get; set; }

    /**
     * The number of clickable objects weighted by difficulty.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("speedNoteCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SpeedNoteCount { get; set; }

    /**
     * The difficulty of the stamina skill.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("stamina")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Stamina { get; set; }

    /**
     * The final star rating.
     */
    [JsonPropertyName("stars")]
    public double Stars { get; set; }
}