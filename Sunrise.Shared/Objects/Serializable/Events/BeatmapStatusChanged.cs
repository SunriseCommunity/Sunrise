using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Beatmaps;

namespace Sunrise.Shared.Objects.Serializable.Events;

public class BeatmapStatusChanged
{
    [JsonPropertyName("beatmap_hash")]
    public string BeatmapHash { get; set; }

    [JsonPropertyName("new_status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BeatmapStatusWeb? NewStatus { get; set; } = null;
}