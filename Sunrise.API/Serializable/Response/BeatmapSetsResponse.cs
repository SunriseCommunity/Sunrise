using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class BeatmapSetsResponse
{
    public BeatmapSetsResponse(List<BeatmapSetResponse> sets, int? totalCount)
    {
        Sets = sets;
        TotalCount = totalCount;
    }

    [JsonConstructor]
    public BeatmapSetsResponse()
    {
    }

    [JsonPropertyName("sets")]
    public List<BeatmapSetResponse> Sets { get; set; }

    [JsonPropertyName("total_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; set; }
}