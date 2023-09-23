using System.Text.Json.Serialization;
using osu.Shared;

namespace Sunrise.Objects.Serializable;

public sealed class DifficultyRequest
{
    [JsonPropertyName("beatmap_id")]
    public int BeatmapId { get; set; }

    [JsonPropertyName("ruleset_id")]
    public int RulesetId { get; set; }

    [JsonPropertyName("mods")]
    public List<object> Mods { get; set; }
}