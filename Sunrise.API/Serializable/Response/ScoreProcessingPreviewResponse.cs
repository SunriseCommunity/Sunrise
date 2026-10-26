using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class ScoreProcessingPreviewResponse
{
    [JsonConstructor]
    public ScoreProcessingPreviewResponse()
    {
    }

    public ScoreProcessingPreviewResponse(AdminScoreResponse score, ScoreProcessingTaskResponse? activeTask)
    {
        Score = score;
        ActiveTask = activeTask;
    }

    [JsonPropertyName("score")]
    public AdminScoreResponse Score { get; set; }

    [JsonPropertyName("active_task")]
    public ScoreProcessingTaskResponse? ActiveTask { get; set; }
}
