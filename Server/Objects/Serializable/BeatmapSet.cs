using System.Text.Json.Serialization;

namespace Sunrise.Server.Objects.Serializable;

public class BeatmapSet
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}