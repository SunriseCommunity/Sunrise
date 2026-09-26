using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Sunrise.Shared.Enums.Scores;

namespace Sunrise.API.Serializable.Request;

public class CreateScoreProcessingTaskRequest
{
    [JsonPropertyName("score_id")]
    [Required]
    [Range(1, int.MaxValue)]
    public int ScoreId { get; set; }

    [JsonPropertyName("action")]
    [Required]
    public ScoreTaskType Action { get; set; }
}
