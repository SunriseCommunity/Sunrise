using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class AdminScoresResponse(List<AdminScoreResponse> scores, int totalCount)
{
    [JsonPropertyName("scores")]
    public List<AdminScoreResponse> Scores { get; set; } = scores;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}
