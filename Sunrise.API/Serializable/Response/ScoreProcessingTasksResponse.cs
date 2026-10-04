using System.Text.Json.Serialization;

namespace Sunrise.API.Serializable.Response;

public class ScoreProcessingTasksResponse(List<ScoreProcessingTaskResponse> tasks, int totalCount)
{
    [JsonPropertyName("tasks")]
    public List<ScoreProcessingTaskResponse> Tasks { get; set; } = tasks;

    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; } = totalCount;
}
