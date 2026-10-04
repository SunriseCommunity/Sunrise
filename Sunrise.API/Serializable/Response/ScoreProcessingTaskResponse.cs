using System.Text.Json.Serialization;
using Sunrise.Shared.Database.Models.Scores;
using Sunrise.Shared.Enums.Scores;
using Sunrise.Shared.Repositories;

namespace Sunrise.API.Serializable.Response;

public class ScoreProcessingTaskResponse
{
    [JsonConstructor]
    public ScoreProcessingTaskResponse()
    {
    }

    public ScoreProcessingTaskResponse(SessionRepository sessionRepository, ScoreProcessingTask task)
    {
        Id = task.Id;
        TaskType = task.TaskType;
        Status = task.Status;
        Priority = task.Priority;
        RetryCount = task.RetryCount;
        ErrorCode = task.ErrorCode;
        ErrorMessage = task.ErrorMessage;
        NextRetryAt = task.NextRetryAt;
        CreatedAt = task.CreatedAt;
        ScoreId = task.ScoreId;
        Score = task.Score != null ? new AdminScoreResponse(sessionRepository, task.Score) : null;
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("task_type")]
    public ScoreTaskType TaskType { get; set; }

    [JsonPropertyName("status")]
    public ScoreProcessingStatus Status { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; }

    [JsonPropertyName("error_code")]
    public ScoreProcessingErrorCode? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("next_retry_at")]
    public DateTime? NextRetryAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("score_id")]
    public int? ScoreId { get; set; }

    [JsonPropertyName("score")]
    public AdminScoreResponse? Score { get; set; }
}
