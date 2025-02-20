using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class BeatmapsResponse(List<BeatmapResponse> beatmaps, int totalCount)
{
    [JsonPropertyName("beatmaps")]
    public List<BeatmapResponse> Beatmaps { get; set; } = beatmaps;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}