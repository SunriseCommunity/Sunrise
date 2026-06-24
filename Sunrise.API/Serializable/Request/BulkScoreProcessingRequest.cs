using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.API.Serializable.Request;

public class BulkScoreProcessingRequest
{
    [JsonPropertyName("score_ids")]
    [Required]
    [MinLength(1)]
    public List<int> ScoreIds { get; set; } = [];

    [JsonPropertyName("action")]
    [Required]
    public ScoreTaskType Action { get; set; }
}
