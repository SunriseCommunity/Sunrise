using System.Text.Json.Serialization;

namespace Sunrise.Server.API.Serializable.Response;

public class BeatmapSetsResponse(List<BeatmapSetResponse> sets, int totalCount)
{
    [JsonPropertyName("sets")] public List<BeatmapSetResponse> Sets { get; set; } = sets;

    [JsonPropertyName("total_count")] public int TotalCount { get; set; } = totalCount;
}