using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class ScoresResponse(List<ScoreResponse> scores, int totalCount)
{
    [JsonPropertyName("scores")]
    public List<ScoreResponse> Scores { get; set; } = scores;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}