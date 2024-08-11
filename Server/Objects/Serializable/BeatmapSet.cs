using System.Text.Json.Serialization;

namespace Sunrise.Server.Objects.Serializable;

public class BeatmapSet
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("artist")]
    public string Artist { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("beatmaps")]
    public Beatmap[] Beatmaps { get; set; }
}