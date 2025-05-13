using System.Text.Json.Serialization;
using Sunrise.Shared.Objects.Serializable;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class HypedBeatmapSetsResponse
{
    public HypedBeatmapSetsResponse(List<HypedBeatmapSetResponse> sets, int? totalCount)
    {
        Sets = sets;
        TotalCount = totalCount;
    }

    [JsonConstructor]
    public HypedBeatmapSetsResponse()
    {
    }

    [JsonPropertyName("sets")]
    public List<HypedBeatmapSetResponse> Sets { get; set; }

    [JsonPropertyName("total_count")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalCount { get; set; }
}