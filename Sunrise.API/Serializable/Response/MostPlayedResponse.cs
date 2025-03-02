using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class MostPlayedResponse(List<MostPlayedBeatmapResponse> mostplayed, int totalCount)
{
    [JsonPropertyName("most_played")]
    public List<MostPlayedBeatmapResponse> MostPlayed { get; set; } = mostplayed;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}