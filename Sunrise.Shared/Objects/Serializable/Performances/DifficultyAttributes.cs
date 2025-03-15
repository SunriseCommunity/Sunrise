using System.Text.Json.Serialization;
using osu.Shared;

namespace Sunrise.Shared.Objects.Serializable.Performances;

public class DifficultyAttributes
{
    /**
     * The difficulty of the aim skill.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("aim")]
    public double? Aim { get; set; }

    /**
     * Weighted sum of aim strains.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("aimDifficultStrainCount")]
    public double? AimDifficultStrainCount { get; set; }

    /**
     * The approach rate.
     * 
     * Only available for osu! and osu!catch.
     */
    [JsonPropertyName("ar")]
    public double? AR { get; set; }

    /**
     * The difficulty of the color skill.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("color")]
    public double? Color { get; set; }

    /**
     * The difficulty of the flashlight skill.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("flashlight")]
    public double? Flashlight { get; set; }

    /**
     * The perceived hit window for an n300 inclusive of rate-adjusting mods (DT/HT/etc)
     * 
     * Only available for osu!taiko and osu!mania.
     */
    [JsonPropertyName("greatHitWindow")]
    public double? GreatHitWindow { get; set; }

    /**
     * The health drain rate.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("hp")]
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
    public double? MonoStaminaFactor { get; set; }

    /**
     * The amount of circles.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nCircles")]
    public int? NCircles { get; set; }

    /**
     * The amount of droplets.
     * 
     * Only available for osu!catch.
     */
    [JsonPropertyName("nDroplets")]
    public int? NDroplets { get; set; }

    /**
     * The amount of fruits.
     * 
     * Only available for osu!catch.
     */
    [JsonPropertyName("nFruits")]
    public int? NFruits { get; set; }

    /**
     * The amount of hold notes in the map.
     * 
     * Only available for osu!mania.
     */
    [JsonPropertyName("nHoldNotes")]
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
    public int? NLargeTicks { get; set; }

    /**
     * The amount of hitobjects in the map.
     * 
     * Only available for osu!mania.
     */
    [JsonPropertyName("nObjects")]
    public int? NObjects { get; set; }

    /**
     * The amount of sliders.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nSliders")]
    public int? NSliders { get; set; }

    /**
     * The amount of spinners.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("nSpinners")]
    public int? NSpinners { get; set; }

    /**
     * The amount of tiny droplets.
     * 
     * Only available for osu!catch.
     */
    [JsonPropertyName("nTinyDroplets")]
    public int? NTinyDroplets { get; set; }

    /**
     * The overall difficulty.
     * 
     * Only available for osu!.
     */
    [JsonPropertyName("od")]
    public double? OD { get; set; }

    /**
     * The perceived hit window for an n100 inclusive of rate-adjusting mods (DT/HT/etc).
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("okHitWindow")]
    public double? OkHitWindow { get; set; }

    /**
     * The difficulty of the hardest parts of the map.
     * 
     * Only available for osu!taiko.
     */
    [JsonPropertyName("peak")]
    public double? Peak { get; set; }

    /**
     * The final star rating.
     */
    [JsonPropertyName("stars")]
    public double Stars { get; set; }
}